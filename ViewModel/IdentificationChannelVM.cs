using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace eLogin.ViewModel
{
    public class IdentificationChannelVM
    {
        public Guid Id { get; set; }
        public string Channel { get; set; }
        public string Key { get; set; }
        public bool IsEnabled { get; set; }
        [DisplayName("Default Entity")]
        public string DefaultIdentifierEntityId { get; set; }
        [DisplayName("Default Property")]
        public string DefaultIdentifierPropertyId { get; set; }
        public string PasswordValidationRegex { get; set; }
        public string PasswordValidationHint { get; set; }
        public int PasswordValidityDays { get; set; }
    }
}
