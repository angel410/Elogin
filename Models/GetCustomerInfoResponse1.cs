﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace eLogin.Models;

public partial class GetCustomerInfoResponse1
{
    public Guid Id { get; set; }

    public Guid RequestId { get; set; }

    public bool IsLocked { get; set; }

    public bool IsSuccess { get; set; }

    public string FailureReason { get; set; }

    public Guid CustomerId { get; set; }

    public DateTime DateTime { get; set; }

    public virtual ICollection<CustomerEntityInstance> CustomerEntityInstances { get; set; } = new List<CustomerEntityInstance>();

    //public virtual ResponseGetCustomerInfoResponse IdNavigation { get; set; }
}