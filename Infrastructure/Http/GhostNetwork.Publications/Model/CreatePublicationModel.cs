/* 
 * GhostNetwork/Publications API
 *
 * No description provided (generated by Openapi Generator https://github.com/openapitools/openapi-generator)
 *
 * The version of the OpenAPI document: 1.0.0
 * 
 * Generated by: https://github.com/openapitools/openapi-generator.git
 */


using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel.DataAnnotations;
using OpenAPIDateConverter = GhostNetwork.Publications.Client.OpenAPIDateConverter;

namespace GhostNetwork.Publications.Model
{
    /// <summary>
    /// CreatePublicationModel
    /// </summary>
    [DataContract]
    public partial class CreatePublicationModel :  IEquatable<CreatePublicationModel>, IValidatableObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CreatePublicationModel" /> class.
        /// </summary>
        [JsonConstructorAttribute]
        protected CreatePublicationModel() { }
        /// <summary>
        /// Initializes a new instance of the <see cref="CreatePublicationModel" /> class.
        /// </summary>
        /// <param name="content">content (required).</param>
        /// <param name="authorId">authorId.</param>
        public CreatePublicationModel(string content = default(string), string authorId = default(string))
        {
            // to ensure "content" is required (not null)
            this.Content = content ?? throw new ArgumentNullException("content is a required property for CreatePublicationModel and cannot be null");
            this.AuthorId = authorId;
        }
        
        /// <summary>
        /// Gets or Sets Content
        /// </summary>
        [DataMember(Name="content", EmitDefaultValue=false)]
        public string Content { get; set; }

        /// <summary>
        /// Gets or Sets AuthorId
        /// </summary>
        [DataMember(Name="authorId", EmitDefaultValue=true)]
        public string AuthorId { get; set; }

        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("class CreatePublicationModel {\n");
            sb.Append("  Content: ").Append(Content).Append("\n");
            sb.Append("  AuthorId: ").Append(AuthorId).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }
  
        /// <summary>
        /// Returns the JSON string presentation of the object
        /// </summary>
        /// <returns>JSON string presentation of the object</returns>
        public virtual string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>
        /// Returns true if objects are equal
        /// </summary>
        /// <param name="input">Object to be compared</param>
        /// <returns>Boolean</returns>
        public override bool Equals(object input)
        {
            return this.Equals(input as CreatePublicationModel);
        }

        /// <summary>
        /// Returns true if CreatePublicationModel instances are equal
        /// </summary>
        /// <param name="input">Instance of CreatePublicationModel to be compared</param>
        /// <returns>Boolean</returns>
        public bool Equals(CreatePublicationModel input)
        {
            if (input == null)
                return false;

            return 
                (
                    this.Content == input.Content ||
                    (this.Content != null &&
                    this.Content.Equals(input.Content))
                ) && 
                (
                    this.AuthorId == input.AuthorId ||
                    (this.AuthorId != null &&
                    this.AuthorId.Equals(input.AuthorId))
                );
        }

        /// <summary>
        /// Gets the hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hashCode = 41;
                if (this.Content != null)
                    hashCode = hashCode * 59 + this.Content.GetHashCode();
                if (this.AuthorId != null)
                    hashCode = hashCode * 59 + this.AuthorId.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// To validate all properties of the instance
        /// </summary>
        /// <param name="validationContext">Validation context</param>
        /// <returns>Validation Result</returns>
        IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        {
            yield break;
        }
    }

}
