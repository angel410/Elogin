﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace eLogin.Models;

public partial class UpdateInfoPropertyRequest
{
    public Guid Id { get; set; }

    public Guid ChannelId { get; set; }

    public string Iv { get; set; }

    public string EncryptedPayload { get; set; }

    public string Type { get; set; }

    public Guid SessionId { get; set; }

    public long UnixTime { get; set; }

    public Guid PropertyId { get; set; }

    public string Name { get; set; }

    public string ValidationRegex { get; set; }

    public string ValidationHint { get; set; }

    public bool IsEncrypted { get; set; }

    public bool IsHashed { get; set; }

    public bool IsUniqueIdentifier { get; set; }

    public DateTime? UpdateInfoPropertyRequestDateTime { get; set; }

    public DateTime DateTime { get; set; }
}