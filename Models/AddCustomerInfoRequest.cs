﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace eLogin.Models;

public partial class AddCustomerInfoRequest
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public long UnixTime { get; set; }

    public Guid CustomerId { get; set; }

    public Guid EntityInstanceId { get; set; }

    public Guid PropertyId { get; set; }

    public string Value { get; set; }

    public DateTime DateTime { get; set; }

}