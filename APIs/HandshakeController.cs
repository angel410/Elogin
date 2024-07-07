using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using eLogin.Data;
using eLogin.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eLogin.APIs
{
    [Route("api/[controller]")]
    [ApiController]
    public class HandshakeController : ControllerBase
    {
        private readonly DatabaseContext _context;

        public HandshakeController(DatabaseContext context)
        {
            _context = context;
        }

        //[HttpPost]
        //public async Task<ActionResult<Session>> PostCustomerLoginAttempt(int channelId, string Hello)
        //{
        //    //var channel = await _context.IdentificationChannel
        //    //    .FirstOrDefaultAsync(c => c.Id == channelId);
        //    //if (channel == null)
        //    //{
        //    //    return NotFound();
        //    //}

        //    //IdentificationChannel channel = new IdentificationChannel
        //    //Session session = new Session();
        //    //session.ChannelId = 
        //    //_context.Session.Add(customerLoginAttempt);
        //    //await _context.SaveChangesAsync();

        //    //return CreatedAtAction("GetCustomerLoginAttempt", new { Id = customerLoginAttempt.Id }, customerLoginAttempt);
        //}
    }
}