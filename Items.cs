using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Autodesk.Forge.Model
{
    [DataContract]
    public partial class Items : IEquatable<Items>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Items" /> class.
        /// </summary>
        [JsonConstructor]
        protected Items() { }
        /// <summary>
        /// Initializes a new instance of the <see cref="Items" /> class.
        /// </summary>
        /// <param name="Jsonapi">Jsonapi (required).</param>
        /// <param name="Links">Links (required).</param>
        /// <param name="Data">Data (required).</param>
        public Items(JsonApiVersion Jsonapi = null, JsonApiLinksSelf Links = null, List<Item> Data = null)
        {
            // to ensure "Jsonapi" is required (not null)
            if (Jsonapi == null)
            {
                throw new InvalidDataException("Jsonapi is a required property for Items and cannot be null");
            }
            else
            {
                this.Jsonapi = Jsonapi;
            }
            // to ensure "Links" is required (not null)
            if (Links == null)
            {
                throw new InvalidDataException("Links is a required property for Items and cannot be null");
            }
            else
            {
                this.Links = Links;
            }
            // to ensure "Data" is required (not null)
            if (Data == null)
            {
                throw new InvalidDataException("Data is a required property for Items and cannot be null");
            }
            else
            {
                this.Data = Data;
            }

            if (Included == null)
            {
                throw new InvalidDataException("Data is a required property for Items and cannot be null");
            }
            else
            {
                this.Included = Included;
            }
        }

        /// <summary>
        /// Gets or Sets Jsonapi
        /// </summary>
        [DataMember(Name = "jsonapi", EmitDefaultValue = false)]
        public JsonApiVersion Jsonapi { get; set; }
        /// <summary>
        /// Gets or Sets Links
        /// </summary>
        [DataMember(Name = "links", EmitDefaultValue = false)]
        public JsonApiLinksSelf Links { get; set; }
        /// <summary>
        /// Gets or Sets Data
        /// </summary>
        [DataMember(Name = "data", EmitDefaultValue = false)]
        public List<Item> Data { get; set; }

        [DataMember(Name = "included", EmitDefaultValue = false)]
        public List<Version> Included { get; set; }
        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("class Items {\n");
            sb.Append("  Jsonapi: ").Append(Jsonapi).Append("\n");
            sb.Append("  Links: ").Append(Links).Append("\n");
            sb.Append("  Data: ").Append(Data).Append("\n");
            sb.Append("  Included: ").Append(Included).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>
        /// Returns the JSON string presentation of the object
        /// </summary>
        /// <returns>JSON string presentation of the object</returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>
        /// Returns true if objects are equal
        /// </summary>
        /// <param name="obj">Object to be compared</param>
        /// <returns>Boolean</returns>
        public override bool Equals(object obj)
        {
            // credit: http://stackoverflow.com/a/10454552/677735
            return this.Equals(obj as TopFolders);
        }

        /// <summary>
        /// Returns true if TopFolders instances are equal
        /// </summary>
        /// <param name="other">Instance of Items to be compared</param>
        /// <returns>Boolean</returns>
        public bool Equals(Items other)
        {
            // credit: http://stackoverflow.com/a/10454552/677735
            if (other == null)
                return false;

            return
                (
                    this.Jsonapi == other.Jsonapi ||
                    this.Jsonapi != null &&
                    this.Jsonapi.Equals(other.Jsonapi)
                ) &&
                (
                    this.Links == other.Links ||
                    this.Links != null &&
                    this.Links.Equals(other.Links)
                ) &&
                (
                    this.Data == other.Data ||
                    this.Data != null &&
                    this.Data.SequenceEqual(other.Data)
                )
                &&
                (
                    this.Included == other.Included ||
                    this.Included != null &&
                    this.Included.SequenceEqual(other.Included)
                );
        }

        /// <summary>
        /// Gets the hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            // credit: http://stackoverflow.com/a/263416/677735
            unchecked // Overflow is fine, just wrap
            {
                int hash = 41;
                // Suitable nullity checks etc, of course :)
                if (this.Jsonapi != null)
                    hash = hash * 59 + this.Jsonapi.GetHashCode();
                if (this.Links != null)
                    hash = hash * 59 + this.Links.GetHashCode();
                if (this.Data != null)
                    hash = hash * 59 + this.Data.GetHashCode();
                if (this.Included != null)
                    hash = hash * 59 + this.Included.GetHashCode();
                return hash;
            }
        }
    }

    public class CreateItemRefsRelationships : CreateItemRelationships
    {
        [DataMember(EmitDefaultValue = false, Name = "refs")]
        public Refs Refs { get; set; }

        public CreateItemRefsRelationships(CreateItemRelationshipsStorage storage = null, Refs refs = null) : base(storage)
        {
            Refs = refs;
        }
    }

    public class CreateVersionRefsRelationships : CreateVersionDataRelationships
    {
        [DataMember(EmitDefaultValue = false, Name = "refs")]
        public Refs Refs { get; set; }

        public CreateVersionRefsRelationships(CreateVersionDataRelationshipsItem item = null,
            CreateItemRelationshipsStorage storage = null, Refs refs = null) : base(item, storage)
        {
            Refs = refs;
        }
    }
}
