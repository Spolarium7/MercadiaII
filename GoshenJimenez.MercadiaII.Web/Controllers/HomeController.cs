using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using GoshenJimenez.MercadiaII.Web.Infrastructure.Data.Helpers;
using GoshenJimenez.MercadiaII.Web.ViewModels.Users;
using GoshenJimenez.MercadiaII.Web.Models;
using GoshenJimenez.MercadiaII.Web.Infrastructure.Data.Models;
using GoshenJimenez.MercadiaII.Web.Infrastructure.Data.Enums;

namespace GoshenJimenez.MercadiaII.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly DefaultDbContext _context;

        public HomeController(DefaultDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(int pageSize = 5, int pageIndex = 1, string keyword = "")
        {
            Page<User> result = new Page<User>();

            if (pageSize < 1)
            {
                pageSize = 1;
            }

            IQueryable<User> userQuery = (IQueryable<User>)this._context.Users;

            if (string.IsNullOrEmpty(keyword) == false)
            {
                userQuery = userQuery.Where(u => u.FirstName.Contains(keyword)
                                            || u.LastName.Contains(keyword)
                                            || u.EmailAddress.Contains(keyword));
            }

            long queryCount = userQuery.Count();

            int pageCount = (int)Math.Ceiling((decimal)(queryCount / pageSize));
            long mod = (queryCount % pageSize);

            if (mod > 0)
            {
                pageCount = pageCount + 1;
            }

            int skip = (int)(pageSize * (pageIndex - 1));
            List<User> users = userQuery.ToList();

            result.Items = users.Skip(skip).Take((int)pageSize).ToList();
            result.PageCount = pageCount;
            result.PageSize = pageSize;
            result.QueryCount = queryCount;
            result.CurrentPage = pageIndex;

            return View(new IndexViewModel()
            {
                Users = result
            });
        }


        [HttpGet, Route("home/create")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost, Route("home/create")]
        public IActionResult Create(CreateUserViewModel model)
        {
            if(!ModelState.IsValid)
                return RedirectToAction("index");

            if(model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError("","Password does not match Password Confirmation");
                return View();
            }

            var user = this._context.Users.FirstOrDefault(u => u.EmailAddress.ToLower() == model.EmailAddress.ToLower());

            if (user == null)
            {
                user = new User()
                {
                    EmailAddress = model.EmailAddress,
                    Password = model.Password,
                    Gender = model.Gender,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                };
                this._context.Users.Add(user);
                this._context.SaveChanges();
            }

            return RedirectToAction("index");
        }


        [HttpGet, Route("home/change-status/{status}/{userId}")]
        public IActionResult ChangeStatus(string status, Guid? userId)
        {
            var loginStatus = (LoginStatus)Enum.Parse(typeof(LoginStatus), status, true);
            var user = this._context.Users.FirstOrDefault(u => u.Id == userId);

            if (user != null)
            {
                user.LoginStatus = loginStatus;
                this._context.Users.Update(user);
                this._context.SaveChanges();
            }

            return RedirectToAction("index");
        }


        [HttpGet, Route("home/reset-password/{userId}")]
        public IActionResult ResetPassword(Guid? userId)
        {
            var user = this._context.Users.FirstOrDefault(u => u.Id == userId);

            if(user != null)
            {
                user.Password = RandomString(8);
                user.LoginStatus = Infrastructure.Data.Enums.LoginStatus.NeedsToChangePassword;
                this._context.Users.Update(user);
                this._context.SaveChanges();
            }

            return RedirectToAction("index");
        }

        private Random random = new Random();
        private string RandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        [HttpGet, Route("home/delete/{userId}")]
        public IActionResult Delete(Guid? userId)
        {
            var user = this._context.Users.FirstOrDefault(u => u.Id == userId);

            if (user != null)
            {
                this._context.Users.Remove(user);
                this._context.SaveChanges();
            }

            return RedirectToAction("index");
        }

        [HttpGet, Route("home/update-profile/{userId}")]
        public IActionResult UpdateProfile(Guid? userId)
        {
            var user = this._context.Users.FirstOrDefault(u => u.Id == userId);

            if (user != null)
            {
                return View(
                    new UpdateProfileViewModel()
                    {
                        UserId = userId,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Gender = user.Gender
                    }                        
                );
            }

            return RedirectToAction("create");
        }

        [HttpPost, Route("home/update-profile")]
        public IActionResult UpdateProfile(UpdateProfileViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = this._context.Users.FirstOrDefault(u => u.Id == model.UserId);

            if (user != null)
            {
                user.Gender = model.Gender;
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                this._context.Users.Update(user);
                this._context.SaveChanges();
            }

            return RedirectToAction("index");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
