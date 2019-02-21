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
using Microsoft.AspNetCore.Http;
using GoshenJimenez.MercadiaII.Web.Infrastructure.Security;
using RestSharp;
using Newtonsoft.Json;
using GoshenJimenez.MercadiaII.Web.Infrastructure.Data.Enums;

namespace GoshenJimenez.MercadiaII.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly DefaultDbContext _context;
        protected readonly IConfiguration _config;
        private string emailUserName;
        private string emailPassword;
        private string facebookAppId;
        private string facebookAppSecret;

        public AccountController(DefaultDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
            var emailConfig = this._config.GetSection("Email");
            emailUserName = (emailConfig["Username"]).ToString();
            emailPassword = (emailConfig["Password"]).ToString();
            var facebookConfig = this._config.GetSection("Facebook");
            facebookAppId = (facebookConfig["AppId"]).ToString();
            facebookAppSecret = (facebookConfig["AppSecret"]).ToString();
        }

        //Register by Email
        [HttpGet, Route("account/register-by-email")]
        public IActionResult RegisterEmail()
        {
            return View();
        }

        [HttpPost, Route("account/register-by-email")]
        public IActionResult RegisterEmail(RegisterViewModel model)
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
                Password = BCrypt.BCryptHelper.HashPassword(model.Password, BCrypt.BCryptHelper.GenerateSalt(8)),
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
                             Welcome to Mercadia II. Please use the following registration code to activate your account: " + registrationCode + @".
                             Regards,
                             Mercadia II",
              model.EmailAddress,
              model.FirstName + " " + model.LastName,
              "Welcome to Mercadia II!!!"
            );

            return RedirectToAction("Verify");
        }

        //Register by Facebook
        [HttpGet, Route("account/{verb}-by-facebook")]
        public ActionResult AuthFacebook(string verb)
        {
            var randomCode = RandomString(6);
            WebUser.SessionCode = randomCode;
            return Redirect("https://www.facebook.com/v2.12/dialog/oauth?client_id=" + facebookAppId + "&redirect_uri=http://localhost:6100/account/get-facebook-access-token&state=" + verb + "-" + randomCode + "&scope=public_profile,email");
        }

        [HttpGet, Route("account/get-facebook-access-token")]
        public ActionResult GetFacebookAccessToken(string code, string state)
        {
            string accessTokenUrl = "https://graph.facebook.com/v2.12/oauth/access_token";
            var client = new RestClient(accessTokenUrl);

            //Access Token
            var request = new RestRequest("", Method.POST);
            request.AddParameter("code", code);
            request.AddParameter("redirect_uri", "http://localhost:6100/account/get-facebook-access-token");
            request.AddParameter("client_id", facebookAppId);
            request.AddParameter("client_secret", facebookAppSecret);

            IRestResponse response = client.Post(request);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var token = JsonConvert.DeserializeObject<Dictionary<string, string>>(response.Content);

                //Account Information (Id,FirstName,LastName,EmailAddress //<LinkedIn has no Gender>)
                client = new RestClient("https://graph.facebook.com/v2.12/me?fields=id,email,first_name,last_name,gender&access_token=" + token["access_token"]);
                request = new RestRequest("", Method.GET);
                request.AddHeader("access_token", token["access_token"]);

                response = client.Get(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var facebookUser = JsonConvert.DeserializeObject<Dictionary<string, string>>(response.Content);
                    var sessionCode = state.Split('-');
                    if (sessionCode[1].ToString() == WebUser.SessionCode)
                    {
                        if (state.Contains("register"))
                        {
                            var duplicate = this._context.Users.FirstOrDefault(u => u.EmailAddress.ToLower() == facebookUser["email"].ToLower());

                            if (duplicate != null)
                            {
                                ModelState.AddModelError("Account is Existing", "Duplicate account");
                                return View();
                            }

                            var registrationCode = RandomString(6);
                            var otp = RandomString(8);
                            User user = new User()
                            {
                                EmailAddress = facebookUser["email"].ToLower(),
                                FirstName = facebookUser["first_name"],
                                LastName = facebookUser["last_name"],
                                Password = BCrypt.BCryptHelper.HashPassword(otp, BCrypt.BCryptHelper.GenerateSalt(8)),
                                Gender = (facebookUser.ContainsKey("gender") ? (facebookUser["gender"].ToLower() == "female" ? Gender.Female : Gender.Male) : Gender.Male),
                                LoginStatus = Infrastructure.Data.Enums.LoginStatus.NeedsToChangePassword,
                                LoginTrials = 0,
                                RegistrationCode = registrationCode,
                                Id = Guid.NewGuid(),
                            };

                            this._context.Users.Add(user);
                            this._context.SaveChanges();

                            //Send email
                            this.SendNow(
                              "Hi " + facebookUser["first_name"] + " " + facebookUser["last_name"] + @",
                             Welcome to Mercadia II. Please use this one-time password to login to your account: " + otp + @".
                             Regards,
                             Mercadia II",
                              facebookUser["email"].ToLower(),
                              facebookUser["first_name"] + " " + facebookUser["last_name"],
                              "Welcome to Mercadia II!!!"
                            );

                            return RedirectToAction("login");
                        }
                        else if (state.Contains("login"))
                        {
                            //Login with emailAddress from Facebook

                        };
                    };
                }

            }

            return View();
        }



        [HttpGet, Route("account/login")]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost, Route("account/login")]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            var user = this._context.Users.FirstOrDefault(u =>
                u.EmailAddress.ToLower() == model.EmailAddress.ToLower());

            if (user != null)
            {
                if (BCrypt.BCryptHelper.CheckPassword(model.Password, user.Password))
                {
                    if (user.LoginStatus == Infrastructure.Data.Enums.LoginStatus.Locked)
                    {
                        ModelState.AddModelError("", "Your account has been locked please contact an Administrator.");
                        return View();
                    }
                    else if (user.LoginStatus == Infrastructure.Data.Enums.LoginStatus.Unverified)
                    {
                        ModelState.AddModelError("", "Please verify your account first.");
                        return View();
                    }
                    else if (user.LoginStatus == Infrastructure.Data.Enums.LoginStatus.NeedsToChangePassword)
                    {
                        user.LoginTrials = 0;
                        user.LoginStatus = Infrastructure.Data.Enums.LoginStatus.Active;
                        this._context.Users.Update(user);
                        this._context.SaveChanges();

                        //SignIn
                        return RedirectToAction("change-password");
                    }
                    else if (user.LoginStatus == Infrastructure.Data.Enums.LoginStatus.Active)
                    {
                        user.LoginTrials = 0;
                        user.LoginStatus = Infrastructure.Data.Enums.LoginStatus.Active;
                        this._context.Users.Update(user);
                        this._context.SaveChanges();

                        //SignIn
                        return RedirectPermanent("/posts/index");
                    }
                }
                else
                {
                    user.LoginTrials = user.LoginTrials + 1;

                    if (user.LoginTrials >= 3)
                    {
                        ModelState.AddModelError("", "Your account has been locked please contact an Administrator.");
                        user.LoginStatus = Infrastructure.Data.Enums.LoginStatus.Locked;
                    }

                    this._context.Users.Update(user);
                    this._context.SaveChanges();

                    ModelState.AddModelError("", "Invalid Login.");
                    return View();
                }
            }

            ModelState.AddModelError("", "Invalid Login.");
            return View();

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
            var fromAddress = new MailAddress(emailUserName, "Mercadia II App");
            string body = message;

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