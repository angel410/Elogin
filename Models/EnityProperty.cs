using System.ComponentModel;

namespace eLogin.Models
{
    public class EntityProperty
    {
        public Guid Id { get; set; }

        [DisplayName("Entity")]
        public Guid? EntityId { get; set; }
        public virtual Entity? Entity { get; set; }

        [DisplayName("Property")]
        public Guid? PropertyId { get; set; }
        public virtual Property? Property { get; set; }
    }
}
