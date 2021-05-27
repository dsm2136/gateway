using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using GhostNetwork.Content.Api;
using GhostNetwork.Content.Client;
using GhostNetwork.Content.Model;
using GhostNetwork.Gateway.Facade;

namespace GhostNetwork.Gateway.Api.NewsFeed
{
    public interface INewsFeedStorage
    {
        INewsFeedReactionsStorage Reactions { get; }
        INewsFeedCommentsStorage Comments { get; }
        
        Task<NewsFeedPublication> GetByIdAsync(string id);
        Task<(IEnumerable<NewsFeedPublication>, long)> GetUserFeedAsync(string userId, int skip, int take);
        Task<NewsFeedPublication> PublishAsync(string content, string userId);
        Task UpdateAsync(string publicationId, string content);
        Task DeleteAsync(string publicationId);
    }

    public interface INewsFeedCommentsStorage
    {
        Task<PublicationComment> GetByIdAsync(string id);
        Task<(IEnumerable<PublicationComment>, long)> GetAsync(string publicationId, int skip, int take);
        Task<PublicationComment> PublishAsync(string content, string publicationId, string userId);
        Task DeleteAsync(string id);
    }

    public class NewsFeedCommentsStorage : INewsFeedCommentsStorage
    {
        private readonly ICommentsApi commentsApi;

        public NewsFeedCommentsStorage(ICommentsApi commentsApi)
        {
            this.commentsApi = commentsApi;
        }

        public async Task<PublicationComment> GetByIdAsync(string id)
        {
            var comment = await commentsApi.GetByIdAsync(id);

            return ToDomain(comment);
        }

        public async Task<(IEnumerable<PublicationComment>, long)> GetAsync(string publicationId, int skip, int take)
        {
            var response = await commentsApi.SearchWithHttpInfoAsync(publicationId, skip, take);

            var comments = response.Data.Select(ToDomain).ToList();
            var totalCount = GetTotalCountHeader(response);

            return (comments, totalCount);
        }

        public async Task<PublicationComment> PublishAsync(string content, string publicationId, string userId)
        {
            var comment = await commentsApi.CreateAsync(new CreateCommentModel(publicationId, content, authorId: userId));

            return ToDomain(comment);
        }

        public async Task DeleteAsync(string id)
        {
            await commentsApi.DeleteAsync(id);
        }

        private static long GetTotalCountHeader(IApiResponse response)
        {
            if (!response.Headers.TryGetValue("X-TotalCount", out var headers))
            {
                return 0;
            }

            return int.TryParse(headers.FirstOrDefault(), out var totalCount)
                ? totalCount
                : 0;
        }

        private static PublicationComment ToDomain(Comment entity)
        {
            return entity == null
                ? null
                : new PublicationComment(
                    entity.Id,
                    entity.Content,
                    entity.PublicationId,
                    ToUser(entity.Author),
                    entity.CreatedOn);
        }

        private static Facade.UserInfo ToUser(Content.Model.UserInfo userInfo)
        {
            return new Facade.UserInfo(userInfo.Id, userInfo.FullName, userInfo.AvatarUrl);
        }
    }

    public class NewsFeedStorage : INewsFeedStorage
    {
        private readonly IPublicationsApi publicationsApi;
        private readonly ICommentsApi commentsApi;
        private readonly IReactionsApi reactionsApi;

        public NewsFeedStorage(IPublicationsApi publicationsApi, ICommentsApi commentsApi, IReactionsApi reactionsApi)
        {
            this.publicationsApi = publicationsApi;
            this.commentsApi = commentsApi;
            this.reactionsApi = reactionsApi;

            Reactions = new NewsFeedReactionsStorage(reactionsApi);
            Comments = new NewsFeedCommentsStorage(commentsApi);
        }

        public INewsFeedReactionsStorage Reactions { get; }
        public INewsFeedCommentsStorage Comments { get; }

        public async Task<NewsFeedPublication> GetByIdAsync(string id)
        {
            try
            {
                var publication = await publicationsApi.GetByIdAsync(id);
                var featuredComments = await LoadCommentsAsync(new[] {id});
                var reactions = await LoadReactionsAsync(new[] {id});
                var userReactions = publication.Author?.Id == null
                    ? new Dictionary<string, UserReaction>()
                    : await LoadUserReactionsAsync(publication.Author?.Id.ToString(), new[] {id});
                
                return new NewsFeedPublication(
                    publication.Id,
                    publication.Content,
                    featuredComments[publication.Id],
                    new ReactionShort(reactions[publication.Id], userReactions[publication.Id]),
                    ToUser(publication.Author));
            }
            catch (ApiException ex) when (ex.ErrorCode == (int)HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<(IEnumerable<NewsFeedPublication>, long)> GetUserFeedAsync(string userId, int skip, int take)
        {
            var publicationsResponse = await publicationsApi.SearchWithHttpInfoAsync(skip, take, order: Ordering.Desc);
            var publications = publicationsResponse.Data;
            var totalCount = GetTotalCountHeader(publicationsResponse);

            var featuredComments = await LoadCommentsAsync(publications.Select(p => p.Id));
            var reactions = await LoadReactionsAsync(publications.Select(p => p.Id));
            var userReactions = userId == null
                ? new Dictionary<string, UserReaction>()
                : await LoadUserReactionsAsync(userId, publications.Select(p => p.Id));

            var news = publications
                .Select(publication => new NewsFeedPublication(
                    publication.Id,
                    publication.Content,
                    featuredComments[publication.Id],
                    new ReactionShort(reactions[publication.Id], userReactions[publication.Id]),
                    ToUser(publication.Author)))
                .ToList();

            return (news, totalCount);
        }

        public async Task<NewsFeedPublication> PublishAsync(string content, string userId)
        {
            var model = new CreatePublicationModel(content, userId);
            var entity = await publicationsApi.CreateAsync(model);

            return new NewsFeedPublication(
                entity.Id,
                entity.Content,
                new CommentsShort(Enumerable.Empty<PublicationComment>(), 0),
                new ReactionShort(new Dictionary<ReactionType, int>(), null),
                ToUser(entity.Author));
        }

        public async Task UpdateAsync(string publicationId, string content)
        {
            await publicationsApi.UpdateAsync(publicationId, new UpdatePublicationModel(content));
        }

        public async Task DeleteAsync(string publicationId)
        {
            await reactionsApi.DeleteAsync(KeysBuilder.PublicationReactionsKey(publicationId));
            // TODO: Remove comments
            await publicationsApi.DeleteAsync(publicationId);
        }

        private static long GetTotalCountHeader(IApiResponse response)
        {
            if (!response.Headers.TryGetValue("X-TotalCount", out var headers))
            {
                return 0;
            }

            return int.TryParse(headers.FirstOrDefault(), out var totalCount)
                ? totalCount
                : 0;
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

        private async Task<Dictionary<string, CommentsShort>> LoadCommentsAsync(IEnumerable<string> publicationIds)
        {
            var query = new FeaturedQuery(publicationIds.ToList());
            var featuredComments = await commentsApi.SearchFeaturedAsync(query);

            return publicationIds
                .ToDictionary(id => id, id =>
                {
                    var comment = featuredComments.GetValueOrDefault(id);

                    return new CommentsShort(
                        comment?.Comments.Select(ToDomain) ?? Enumerable.Empty<PublicationComment>(),
                        comment?.TotalCount ?? 0);
                });
        }

        private async Task<Dictionary<string, Dictionary<ReactionType, int>>> LoadReactionsAsync(IEnumerable<string> publicationIds)
        {
            var query = new ReactionsQuery
            {
                Keys = publicationIds.Select(KeysBuilder.PublicationReactionsKey).ToList()
            };
            var reactions = await reactionsApi.GetGroupedReactionsAsync(query);

            return publicationIds
                .ToDictionary(id => id, id =>
                {
                    var r = reactions.ContainsKey(KeysBuilder.PublicationReactionsKey(id))
                        ? reactions[KeysBuilder.PublicationReactionsKey(id)]
                        : new Dictionary<string, int>();
                    return r.Keys
                        .Select(k => (Enum.Parse<ReactionType>(k), r[k]))
                        .ToDictionary(o => o.Item1, o => o.Item2);
                });
        }

        private async Task<Dictionary<string, UserReaction>> LoadUserReactionsAsync(string userId, IEnumerable<string> publicationIds)
        {
            var query = new ReactionsQuery
            {
                Keys = publicationIds.Select(id => $"publication_{id}").ToList()
            };
            var userReactionsResponse = await reactionsApi.SearchAsync(userId, query);
            var userReactions = userId == null
                ? new Dictionary<string, Reaction>()
                : userReactionsResponse
                    .GroupBy(r => r.Key, r => r)
                    .ToDictionary(k => k.Key, k => k.First());

            return publicationIds
                .ToDictionary(id => id, id => userReactions.ContainsKey($"publication_{id}")
                    ? new UserReaction(Enum.Parse<ReactionType>(userReactions[$"publication_{id}"].Type))
                    : null);
        }

        private static Facade.UserInfo ToUser(Content.Model.UserInfo userInfo)
        {
            return new Facade.UserInfo(userInfo.Id, userInfo.FullName, userInfo.AvatarUrl);
        }
    }
}