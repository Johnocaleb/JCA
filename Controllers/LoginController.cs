using System.ComponentModel.DataAnnotations;
using CHAT.DatabaseAccess;
using CHAT.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CHAT.Processor;
using SharpCompress.Crypto;

namespace CHAT.Controllers
{
    public class LoginController : Controller
    {
        private readonly UserDatabaseAccess _userDatabaseAccess;
        public LoginController(UserDatabaseAccess userDatabaseAccess)
        {
            _userDatabaseAccess = userDatabaseAccess;
        }

        [HttpGet, AllowAnonymous]
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Environment");

            var checkuser = _userDatabaseAccess.QueryOne(p => p.Email == "calebjohn@gmail.com");
            if (checkuser == null)
            {
                var newUser = new UserModel
                {
                    Id = Guid.NewGuid(),

                    Surname = "caleb",
                    FirstName = "john",
                    Email = "calebjohn@gmail.com",
                    TelephoneNumber = "08036000305",
                    RegistrationDate = DateTime.Now,
                    IsActive = true,
                    Password = BCrypt.Net.BCrypt.EnhancedHashPassword("1000"),

                };
                _userDatabaseAccess.Insert(newUser);
            }

            return View();
        }

        [HttpPost, AllowAnonymous]
        public async Task<IActionResult> Login(UserModel login) {
            login.Email = login.Email.ToLowerInvariant();

            var checkUser = _userDatabaseAccess
                .QueryOne(c => c.Email == login.Email);
            if (checkUser == null)
            {
                ViewBag.ErrorMessage = "User does not exist, Please Sign Up!";
                return View();
            }

            if (!BCrypt.Net.BCrypt.EnhancedVerify(login.Password,
                    checkUser.Password))
            {
                ViewBag.ErrorMessage = "user's Password is wrong!";
                return View();
            }

            List<Claim> claims = new() {
                new Claim(ClaimTypes.Email, checkUser.Email),
                new Claim("FirstName", checkUser.FirstName),
                new Claim("Surname", checkUser.Surname)
            };


            ClaimsIdentity claimIdentity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            ClaimsPrincipal claimPrincipal = new(claimIdentity);
            await HttpContext.SignInAsync(claimPrincipal);
            return RedirectToAction("Index", "Environment");
        }

        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public IActionResult Register(UserModel register, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(register.Password))
            {
                ViewBag.Message = "Password cannot be empty!";
                return View();
            }

            if (string.IsNullOrWhiteSpace(register.Surname) ||
                string.IsNullOrWhiteSpace(register.FirstName) ||
                string.IsNullOrWhiteSpace(register.TelephoneNumber))
            {
                ViewBag.Message = "Surname or Firstname or Telephone " +
                                  "number cannot be empty!";
                return View();
            }

            if (register.Password != confirmPassword)
            {
                ViewBag.Message = "Confirm Password and Password must be the same!";
                return View();
            }

            if (!Functions.IsEmailValid(register.Email))
            {
                ViewBag.Message = "This Email is not valid!";
                return View();
            }


            register.Email = register.Email.ToLowerInvariant();
            var checkIfUserExist = _userDatabaseAccess
                .QueryOne(p => p.Email == register.Email);
            if (checkIfUserExist == null)
            {
                register.Id = Guid.NewGuid();
                register.IsActive = true;
                register.RegistrationDate = DateTime.Now;
                register.Password = BCrypt.Net.BCrypt.EnhancedHashPassword(register.Password);
                _userDatabaseAccess.Insert(register);
                ViewBag.Message = "User is added successfully";
                return View("Login");
            }

            return View();
        }

        public async Task<IActionResult> LogOut()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Login");
        }
    }
}
