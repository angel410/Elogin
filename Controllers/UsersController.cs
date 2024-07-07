using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using eLogin.Data;
using eLogin.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using eLogin.Identity;
using Microsoft.AspNetCore.Identity;
using eLogin.Models.Roles;

namespace eLogin.Controllers
{
    //[Authorize(Roles = "eLoginAdmin")]
    [Authorize(Roles = nameof(eLoginAdmin))]
    public class UsersController : Controller
    {
        private readonly UserManager<User> UserManager;

        private readonly DatabaseContext _context;

        public UsersController(DatabaseContext context, UserManager<User> userManager)
        {
            _context = context;
            UserManager = userManager;
        }

        // GET: Users
        public async Task<IActionResult> Index()
        {
            var UserList = await _context.Users.ToListAsync();
            foreach(User u in UserList)
            {
                var currentRoles = await UserManager.GetRolesAsync(u);
                u.Roles = currentRoles.ToArray();
            }
            return View(UserList);
        }

        // GET: Users/Details/5
        public async Task<IActionResult> Details(Guid id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(m => m.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Users/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("EmailAddress,Id,UserName,NormalizedUserName,Email,NormalizedEmail,EmailConfirmed,PasswordHash,SecurityStamp,ConcurrencyStamp,PhoneNumber,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnd,LockoutEnabled,AccessFailedCount")] User user)
        {
            if (ModelState.IsValid)
            {
                user.Id = Guid.NewGuid();
                _context.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(user);
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(Guid id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var Roles = await UserManager.GetRolesAsync(user);
            
            
            user.Roles = Roles.ToArray();
            var AllRoles= await _context.Roles.Select(Role => Role.Name).ToArrayAsync();
            ViewData.Add("Roles", AllRoles);

            return View(user);
        }

        

        // POST: Users/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("EmailAddress,Id,UserName,NormalizedUserName,Email,NormalizedEmail,EmailConfirmed,PasswordHash,SecurityStamp,ConcurrencyStamp,PhoneNumber,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnd,LockoutEnabled,AccessFailedCount,Roles")] User user)
        {
            if (id != user.Id)
            {
                return NotFound();
            }

            //if (ModelState.IsValid)
            //{
                try
                {
                    //_context.Update(user);
                    //await _context.SaveChangesAsync();

                    var Roles = await UserManager.GetRolesAsync(user);

                    //var NewRoles = user.Roles;

                    //user.Roles = Roles.ToArray();

                    var CurrentRoles = await UserManager.GetRolesAsync(user);
                    string[] currentRollesArray = CurrentRoles.ToArray();

                    foreach (var cr in currentRollesArray)
                    {
                        if (!user.Roles.Contains(cr))
                        {
                            await UserManager.RemoveFromRoleAsync(user, cr);
                        }

                    }


                    foreach (var r in user.Roles)
                    {
                        if (!currentRollesArray.Contains(r))
                        {
                            await UserManager.AddToRoleAsync(user, r);
                        }
                    }

                    await _context.SaveChangesAsync();

                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(user.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            //}
           // return View(user);

            

        }

        // GET: Users/Delete/5
        public async Task<IActionResult> Delete(Guid id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(m => m.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool UserExists(Guid id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}
