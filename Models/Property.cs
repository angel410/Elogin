﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace eLogin.Models;

public partial class Property
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public string? ValidationRegex { get; set; }

    public string? ValidationHint { get; set; }

    public bool? IsEncrypted { get; set; }

    public bool? IsHashed { get; set; }

    public bool? IsUniqueIdentifier { get; set; }

    public bool? IsRequired { get; set; }

    [JsonIgnore]
    public bool? IsDeleted { get; set; }

    public virtual List<CustomerInfoValue> CustomerInfoValues { get; set; }
    public virtual List<EntityProperty> EntityProperties { get; set; }
    public virtual List<ChannelLoginProperty> ChannelLoginProperties { get; set; }
}