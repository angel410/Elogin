using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using eLogin.Data;
using eLogin.Models;

namespace eLogin.APIs
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerLoginAttemptsController : ControllerBase
    {
        private readonly DatabaseContext _context;

        public CustomerLoginAttemptsController(DatabaseContext context)
        {
            _context = context;
        }

        // GET: api/CustomerLoginAttempts
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerLoginAttempt>>> GetCustomerLoginAttempt()
        {
            return await _context.CustomerLoginAttempt.ToListAsync();
        }

        // GET: api/CustomerLoginAttempts/5
        [HttpGet("{Id}")]
        public async Task<ActionResult<CustomerLoginAttempt>> GetCustomerLoginAttempt(int Id)
        {
            var customerLoginAttempt = await _context.CustomerLoginAttempt.FindAsync(Id);

            if (customerLoginAttempt == null)
            {
                return NotFound();
            }

            return customerLoginAttempt;
        }

        // PUT: api/CustomerLoginAttempts/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPut("{Id}")]
        public async Task<IActionResult> PutCustomerLoginAttempt(Guid Id, CustomerLoginAttempt customerLoginAttempt)
        {
            if (Id != customerLoginAttempt.Id)
            {
                return BadRequest();
            }

            _context.Entry(customerLoginAttempt).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CustomerLoginAttemptExists(Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/CustomerLoginAttempts
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPost]
        public async Task<ActionResult<CustomerLoginAttempt>> PostCustomerLoginAttempt(CustomerLoginAttempt customerLoginAttempt)
        {
            _context.CustomerLoginAttempt.Add(customerLoginAttempt);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetCustomerLoginAttempt", new { Id = customerLoginAttempt.Id }, customerLoginAttempt);
        }

        // DELETE: api/CustomerLoginAttempts/5
        [HttpDelete("{Id}")]
        public async Task<ActionResult<CustomerLoginAttempt>> DeleteCustomerLoginAttempt(int Id)
        {
            var customerLoginAttempt = await _context.CustomerLoginAttempt.FindAsync(Id);
            if (customerLoginAttempt == null)
            {
                return NotFound();
            }

            _context.CustomerLoginAttempt.Remove(customerLoginAttempt);
            await _context.SaveChangesAsync();

            return customerLoginAttempt;
        }

        private bool CustomerLoginAttemptExists(Guid Id)
        {
            return _context.CustomerLoginAttempt.Any(e => e.Id == Id);
        }
    }
}
