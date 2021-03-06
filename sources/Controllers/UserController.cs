﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;

using SetMeta.Web.Helpers;
using SetMeta.Web.Services;
using SetMeta.Web.Models;

namespace SetMeta.Web.Controllers
{
    public class UserController : BaseController
    {
        private readonly IFormsAuthenticationService _formsAuthenticationService;
        private readonly IUserService _userService;
        private readonly IAppService _appService;

        public UserController(
            IFormsAuthenticationService formsAuthenticationService,
            IUserService userService,
            IAppService appService)
        {
            _formsAuthenticationService = formsAuthenticationService;
            _userService = userService;
            _appService = appService;
        }

        [HttpGet]
        public async Task<ActionResult> Apps(string id, int page = 1)
        {
            var pageNumber = page;
            if (pageNumber < 1)
            {
                pageNumber = 1;
            }

            if (string.IsNullOrEmpty(id))
            {
                id = User.Identity.GetId();
            }

            ViewBag.UserId = id;

            var apps = await _appService.GetByUserId(id, pageNumber);
            if (apps == null) return RedirectToHome();
            
            var list = apps.Items.Select(AppModel.Map).ToList();
            var model = new PageModel<AppModel>
            {
                Items = list,
                HasNextPage = apps.HasNextPage,
                HasPrevPage = apps.HasPreviousPage,
                PageNumber = apps.Number,
                ItemCount = apps.TotalCount,
                PageCount = apps.TotalPageCount
            };

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<JsonResult> ChangeStatus(string id, bool isActive)
        {
            var model = new ResponseModel { IsOk = false };
            if (string.IsNullOrEmpty(id))
            {
                return Json(model, JsonRequestBehavior.DenyGet);
            }

            model.IsOk = await _userService.ChangeStatus(id, isActive);
            return Json(model, JsonRequestBehavior.DenyGet);
        }

        #region Membership
        [HttpGet, AllowAnonymous]
        public ActionResult New()
        {
            var model = new UserModel();
            return View(model);
        }
        [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
        public async Task<ActionResult> New(UserModel model)
        {
            if (!model.IsValid())
            {
                SetPleaseTryAgain(model);
                return View(model);
            }

            model.Language = Thread.CurrentThread.CurrentUICulture.Name;
            model.Id = Guid.NewGuid().ToNoDashString();

            var isCreated = await _userService.Create(model, ConstHelper.Developer);
            if (!isCreated)
            {
                SetPleaseTryAgain(model);
                return View(model);
            }

            _formsAuthenticationService.SignIn(model.Id, model.Name, model.Email, ConstHelper.Developer, true);

            return Redirect("/user/apps");
        }

        [HttpGet, AllowAnonymous]
        public ActionResult PasswordReset()
        {
            var model = new PasswordResetModel();
            if (User.Identity.IsAuthenticated)
            {
                model.Email = User.Identity.GetEmail();
            }

            return View(model);
        }

        [HttpGet, AllowAnonymous]
        public ActionResult Login()
        {
            var model = new LoginModel();
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
        public async Task<ActionResult> Login(LoginModel model)
        {
            if (!model.IsValid())
            {
                SetPleaseTryAgain(model);
                return View(model);
            }

            var authenticated = await _userService.Authenticate(model.Email, model.Password);
            if (!authenticated)
            {
                SetPleaseTryAgain(model);
                return View(model);
            }

            var user = await _userService.GetByEmail(model.Email);
            if (user == null)
            {
                SetPleaseTryAgain(model);
                return View(model);
            }

            _formsAuthenticationService.SignIn(user.PublicId, user.Name, user.Email, user.RoleName, true);

            if (!string.IsNullOrEmpty(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return Redirect("/user/apps");
        }

        [HttpGet]
        public ActionResult Logout()
        {
            _formsAuthenticationService.SignOut();
            return RedirectToHome();
        }
        #endregion
    }
}