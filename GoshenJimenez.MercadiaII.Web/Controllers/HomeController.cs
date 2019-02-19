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
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace GoshenJimenez.MercadiaII.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly DefaultDbContext _context;
        protected readonly IConfiguration _config;
        private string emailUserName;
        private string emailPassword;

        public HomeController(DefaultDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
            var emailConfig = this._config.GetSection("Email");
            emailUserName = (emailConfig["Username"]).ToString();
            emailPassword = (emailConfig["Password"]).ToString();
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
                var registrationCode = RandomString(6);
                user = new User()
                {
                    EmailAddress = model.EmailAddress.ToLower(),
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Password = BCrypt.BCryptHelper.HashPassword(model.Password, BCrypt.BCryptHelper.GenerateSalt(8)),
                    Gender = model.Gender,
                    LoginStatus = Infrastructure.Data.Enums.LoginStatus.Unverified,
                    LoginTrials = 0,
                    RegistrationCode = registrationCode,
                    Id = Guid.NewGuid(),
                };
                this._context.Users.Add(user);
                this._context.SaveChanges();


                this.SendNow(
                  "Hi " + model.FirstName + " " + model.LastName + @",
                                             Welcome to Mercadia II. Please use the following registration code to activate your account: " + registrationCode + @".
                                             Regards,
                                             Mercadia II",
                  model.EmailAddress,
                  model.FirstName + " " + model.LastName,
                  "Welcome to Mercadia II!!!"
                );
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

        private void SendNow(string message, string messageTo, string messageName, string emailSubject)
        {
            var fromAddress = new MailAddress(emailUserName, "CSM Bataan Apps");
            string body = message;


            ///https://support.google.com/accounts/answer/6010255?hl=en
            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, emailPassword),
                Timeout = 20000
            };

            var toAddress = new MailAddress(messageTo, messageName);

            smtp.Send(new MailMessage(fromAddress, toAddress)
            {
                Subject = emailSubject,
                Body = body,
                IsBodyHtml = true
            });
        }
    }
}
