﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace eLogin.Models;

public partial class SearchCustomerResponse
{
    [JsonIgnore]
    public Guid Id { get; set; }

    [JsonIgnore]
    public Guid RequestId { get; set; }

    [JsonIgnore]
    public string RequestType { get; set; }

    public virtual List<MatchingCustomer> MatchingCustomers { get; set; }

    public bool IsSuccess { get; set; }

    public string FailureReason { get; set; }

    public DateTime DateTime { get; set; } = DateTime.UtcNow;
}