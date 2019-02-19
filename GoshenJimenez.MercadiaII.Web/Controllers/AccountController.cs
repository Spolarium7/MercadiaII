using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using GoshenJimenez.MercadiaII.Web.Infrastructure.Data.Helpers;
using GoshenJimenez.MercadiaII.Web.Infrastructure.Data.Models;
using GoshenJimenez.MercadiaII.Web.ViewModels.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace GoshenJimenez.MercadiaII.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly DefaultDbContext _context;
        protected readonly IConfiguration _config;
        private string emailUserName;
        private string emailPassword;

        public AccountController(DefaultDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
            var emailConfig = this._config.GetSection("Email");
            emailUserName = (emailConfig["Username"]).ToString();
            emailPassword = (emailConfig["Password"]).ToString();
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if(model.Password == model.ConfirmPassword)
            {
                return View(model);
            }

            var duplicate = this._context.Users.FirstOrDefault(u => u.EmailAddress.ToLower() == model.EmailAddress.ToLower());

            if(duplicate != null)
            {
                return View(model);
            }
            var registrationCode = RandomString(6);
            User user = new User()
            {
                EmailAddress = model.EmailAddress.ToLower(),
                FirstName = model.FirstName,
                LastName = model.LastName,
                Password = model.Password,
                Gender = model.Gender,
                LoginStatus = Infrastructure.Data.Enums.LoginStatus.Unverified,
                LoginTrials = 0,
                RegistrationCode = registrationCode,
                Id = Guid.NewGuid(),
            };

            this._context.Users.Add(user);
            this._context.SaveChanges();

            //Send email
            this.SendNow(
              "Hi " + model.FirstName + " " + model.LastName + @",
                             Welcome to CSM Bataan Website. Please use the following registration code to activate your account: " + registrationCode + @".
                             Regards,
                             CSM Bataan Website",
              model.EmailAddress,
              model.FirstName + " " + model.LastName,
              "Welcome to CSM Bataan Website!!!"
            );

            return RedirectToAction("Verify");
        }
        [HttpGet, Route("account/verify")]
        public IActionResult Verify()
        {
            return View();
        }

        [HttpPost, Route("account/verify")]
        public IActionResult Verify(VerifyViewModel model)
        {
            var user = this._context.Users.FirstOrDefault(u => u.EmailAddress.ToLower() == model.EmailAddress.ToLower() && u.RegistrationCode == model.RegistrationCode);

            if (user != null)
            {
                user.LoginStatus = Infrastructure.Data.Enums.LoginStatus.Active;
                user.LoginTrials = 0;
                this._context.Users.Update(user);
                this._context.SaveChanges();

                return RedirectToAction("login");
            }

            return View();
        }


        private Random random = new Random();
        private string RandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
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