using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Newtonsoft.Json;

namespace eLogin.Models
{
    public class Response<T>
    {
        [JsonIgnore]
        public Guid Id { get; set; }

        [Required]
        public Guid RequestId { get; set; }

        [Required]
        public string IV { get; set; }

        [Required]
        public string EncryptedPayload { get; set; }

        [JsonIgnore, NotMapped]
        public virtual T Payload { get; set; }
        public int PayloadId { get; set; } // Foreign key property


        [JsonIgnore]
        public DateTime DateTime { get; set; } = DateTime.UtcNow;

        public void Encrypt(in string Key)
        {
            var Json = JsonConvert.SerializeObject(Payload);
            string JsonString = Convert.ToBase64String(Encoding.UTF8.GetBytes(Json));
            EncryptedPayload = Cryptography.AES(in Key, IV, in JsonString, Cryptography.Operation.Encrypt);
        }
    }
}
