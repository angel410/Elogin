using System.ComponentModel;

namespace eLogin.ViewModel
{
    public class EntityPropertyVM
    {
        public Guid Id { get; set; }
        [DisplayName("Entity")]
        public string EntityName { get; set; }
        [DisplayName("Property")]
        public string PropertyName { get; set; }
    }
}
