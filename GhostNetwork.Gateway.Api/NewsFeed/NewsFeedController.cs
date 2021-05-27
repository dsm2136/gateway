using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using GhostNetwork.Content.Client;
using GhostNetwork.Content.Model;
using GhostNetwork.Gateway.Facade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Filters;

namespace GhostNetwork.Gateway.Api.NewsFeed
{
    [Route("[controller]")]
    [ApiController]
    [Authorize]
    public class NewsFeedController : ControllerBase
    {
        private readonly INewsFeedStorage newsFeedStorage;
        private readonly CurrentUserProvider currentUserProvider;

        public NewsFeedController(INewsFeedStorage newsFeedStorage, CurrentUserProvider currentUserProvider)
        {
            this.newsFeedStorage = newsFeedStorage;
            this.currentUserProvider = currentUserProvider;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [SwaggerResponseHeader(StatusCodes.Status200OK, Consts.Headers.TotalCount, "number", "")]
        [SwaggerResponseHeader(StatusCodes.Status200OK, Consts.Headers.HasMore, "boolean", "")]
        public async Task<ActionResult<IEnumerable<NewsFeedPublication>>> GetAsync(
            [FromQuery, Range(0, int.MaxValue)] int skip = 0,
            [FromQuery, Range(1, 50)] int take = 20)
        {
            var (news, totalCount) = await newsFeedStorage.GetUserFeedAsync(currentUserProvider.UserId, skip, take);

            Response.Headers.Add(Consts.Headers.TotalCount, totalCount.ToString());
            Response.Headers.Add(Consts.Headers.HasMore, (skip + take < totalCount).ToString());

            return Ok(news);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<ActionResult<NewsFeedPublication>> CreateAsync(
            [FromBody] CreateNewsFeedPublication content)
        {
            var model = new CreatePublicationModel(content.Content, currentUserProvider.UserId);
            var entity = await publicationsApi.CreateAsync(model);

            var backModel = new NewsFeedPublication(
                entity.Id,
                entity.Content,
                new CommentsShort(Enumerable.Empty<PublicationComment>(), 0),
                new ReactionShort(new Dictionary<ReactionType, int>(), null),
                ToUser(entity.Author));

            return Created(string.Empty, backModel);
        }

        [HttpPut("{publicationId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> UpdateAsync(
            [FromRoute] string publicationId,
            [FromBody] CreateNewsFeedPublication model)
        {
            var publication = await newsFeedStorage.GetByIdAsync(publicationId);

            if (publication == null)
            {
                return NotFound();
            }

            if (publication.Author.Id.ToString() != currentUserProvider.UserId)
            {
                return Forbid();
            }

            try
            {
                await publicationsApi.UpdateAsync(publicationId, new UpdatePublicationModel(model.Content));
            }
            catch (ApiException ex) when (ex.ErrorCode == (int) HttpStatusCode.NotFound)
            {
                return NotFound();
            }

            return NoContent();
        }

        [HttpDelete("{publicationId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteAsync([FromRoute] string publicationId)
        {
            var publication = await newsFeedStorage.GetByIdAsync(publicationId);

            if (publication == null)
            {
                return NotFound();
            }

            if (publication.Author.Id.ToString() != currentUserProvider.UserId)
            {
                return Forbid();
            }

            await reactionsApi.DeleteAsync($"publication_${publicationId}");
            await publicationsApi.DeleteAsync(publicationId);

            return NoContent();
        }

        [HttpPost("{publicationId}/reaction")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<ReactionShort>> AddReactionAsync(
            [FromRoute] string publicationId,
            [FromBody] AddNewsFeedReaction model)
        {
            if (await newsFeedStorage.GetByIdAsync(publicationId) == null)
            {
                return NotFound();
            }

            var result = await reactionsApi.UpsertAsync($"publication_{publicationId}", model.Reaction.ToString(), currentUserProvider.UserId);

            return Ok(await ToReactionShort(publicationId, result));
        }

        [HttpDelete("{publicationId}/reaction")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> RemoveReactionAsync(
            [FromRoute] string publicationId)
        {
            if (await newsFeedStorage.GetByIdAsync(publicationId) == null)
            {
                return NotFound();
            }

            var result = await reactionsApi.DeleteByAuthorAsync($"publication_{publicationId}", currentUserProvider.UserId);

            return Ok(await ToReactionShort(publicationId, result));
        }

        [HttpGet("{publicationId}/comments")]
        [SwaggerResponseHeader(StatusCodes.Status200OK, Consts.Headers.TotalCount, "number", "")]
        [SwaggerResponseHeader(StatusCodes.Status200OK, Consts.Headers.HasMore, "boolean", "")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> SearchCommentsAsync(
            [FromRoute] string publicationId,
            [FromQuery, Range(0, int.MaxValue)] int skip,
            [FromQuery, Range(0, 100)] int take = 10)
        {
            var response = await commentsApi.SearchWithHttpInfoAsync(publicationId, skip, take);

            var totalCount = GetTotalCountHeader(response);

            Response.Headers.Add(Consts.Headers.TotalCount, totalCount.ToString());
            Response.Headers.Add(Consts.Headers.HasMore, (skip + take < totalCount).ToString());

            return Ok(response.Data.Select(ToDomain).ToList());
        }

        [HttpPost("{publicationId}/comments")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<ActionResult<PublicationComment>> AddCommentAsync(
            [FromRoute] string publicationId,
            [FromBody] AddNewsFeedComment model)
        {
            if (await newsFeedStorage.GetByIdAsync(publicationId) == null)
            {
                return NotFound();
            }

            var comment = await commentsApi.CreateAsync(new CreateCommentModel(publicationId, model.Content, authorId: currentUserProvider.UserId));

            return Created(string.Empty, new PublicationComment(comment.Id, comment.Content, comment.PublicationId, ToUser(comment.Author), comment.CreatedOn));
        }

        [HttpDelete("comments/{commentId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PublicationComment>> DeleteCommentAsync([FromRoute] string commentId)
        {
            try
            {
                var comment = await commentsApi.GetByIdAsync(commentId);

                if (comment.Author.Id.ToString() != currentUserProvider.UserId)
                {
                    return Forbid();
                }
            }
            catch (ApiException ex) when (ex.ErrorCode == (int)HttpStatusCode.NotFound)
            {
                return NotFound();
            }

            await commentsApi.DeleteAsync(commentId);

            return NoContent();
        }

        private static Facade.UserInfo ToUser(Content.Model.UserInfo userInfo)
        {
            return new Facade.UserInfo(userInfo.Id, userInfo.FullName, userInfo.AvatarUrl);
        }

        private static long GetTotalCountHeader(IApiResponse response)
        {
            var totalCount = 0;
            if (response.Headers.TryGetValue("X-TotalCount", out var headers))
            {
                if (!int.TryParse(headers.FirstOrDefault(), out totalCount))
                {
                    totalCount = 0;
                }
            }

            return totalCount;
        }

        private static PublicationComment ToDomain(Comment entity)
        {
            return new PublicationComment(
                entity.Id,
                entity.Content,
                entity.PublicationId,
                ToUser(entity.Author),
                entity.CreatedOn);
        }

        private async Task<ReactionShort> ToReactionShort(string publicationId, Dictionary<string, int> response)
        {
            var reactions = response.Keys
                    .Select(k => (Enum.Parse<ReactionType>(k), response[k]))
                    .ToDictionary(o => o.Item1, o => o.Item2);

            UserReaction userReaction = null;

            if (currentUserProvider.UserId != null)
            {
                try
                {
                    var reactionByAuthor = await reactionsApi.GetReactionByAuthorAsync($"publication_{publicationId}", currentUserProvider.UserId);

                    userReaction = new UserReaction(Enum.Parse<ReactionType>(reactionByAuthor.Type));
                }
                catch (ApiException ex) when (ex.ErrorCode == (int)HttpStatusCode.NotFound)
                {
                    // ignored
                }
            }

            return new ReactionShort(reactions, userReaction);
        }
    }
}
