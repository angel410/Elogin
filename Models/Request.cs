using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace eLogin.Models
{
    public class Request<T>
    {
        public Guid Id { get; set; }

        [Required]
        public Guid ChannelId { get; set; }

        [Required]
        public string IV { get; set; }

        [Required]
        public string EncryptedPayload { get; set; }

        public string Type { get; set; } = typeof(T).Name;

        [JsonIgnore, NotMapped]
        public virtual T Payload { get; set; }
        public int PayloadId { get; set; } // Foreign key property


        [JsonIgnore]
        public DateTime DateTime { get; set; } = DateTime.UtcNow;

        // Foreign key
     

        public void Decrypt(in string Key)
        {           
            var Json = Cryptography.AES(in Key, IV, EncryptedPayload, Cryptography.Operation.Decrypt);
            Payload = JsonConvert.DeserializeObject<T>(Json);
        }
    }
}
