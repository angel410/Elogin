﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace eLogin.Models;

public partial class RegisterResponse
{
    public Guid Id { get; set; }

    public Guid ResponseRegisterResponseRequestId { get; set; }

    public string Iv { get; set; }

    public string EncryptedPayload { get; set; }

    public Guid RequestId { get; set; }

    public bool IsSuccess { get; set; }

    public string FailureReason { get; set; }

    public Guid CustomerId { get; set; }

    public DateTime? DateTime { get; set; }

    public DateTime ResponseRegisterResponseDateTime { get; set; }
}