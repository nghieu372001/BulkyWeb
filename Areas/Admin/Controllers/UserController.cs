using Bulky.DataAccess.Data;
using Bulky.DataAccess.Repository;
using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IO;

namespace BulkyWeb.Areas.Admin.Controllers //namespace = name project (BulkyWeb) + name location folder (Areas/Admin/Controllers)
{
    [Area("Admin")] //Area attribute: tell UserController belongs to area Admin 
    [Authorize(Roles = StaticDetail.Role_Admin)] //Chỉ có Admin mới có thể thực hiện các chức năng của User. Áp dụng cho toàn bộ property và method class UserController
    //Có thể sử dụng [Authorize] cho từng method và property
    public class UserController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IUnitOfWork _unitOfWork;

        public UserController(UserManager<IdentityUser> userManager, IUnitOfWork unitOfWork, RoleManager<IdentityRole> roleManager)
        {
            _unitOfWork = unitOfWork;
            _roleManager = roleManager;
            _userManager = userManager;
        }

        //Sử dụng cho View - GET
        public IActionResult Index()
        {
            return View();
        }

        //Sử dụng cho View - GET
        public IActionResult RoleManage(string userId) //userId vì bên user.js đang truyền a href="/admin/user/RoleManage?userId=${data.id}
        {
            RoleManageViewModel RoleViewModel = new RoleManageViewModel()
            {
                ApplicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId, includeProperties: "Company"),
                RoleList = _roleManager.Roles.Select(i => new SelectListItem
                {
                    Text = i.Name,
                    Value = i.Name
                }),
                CompanyList = _unitOfWork.Company.GetAll().Select(i => new SelectListItem //Select giống Map Array bên JS
                {
                    Text = i.Name,
                    Value = i.Id.ToString()
                }),
            };

            RoleViewModel.ApplicationUser.Role = _userManager.GetRolesAsync(_unitOfWork.ApplicationUser.Get(u => u.Id == userId)).GetAwaiter().GetResult().FirstOrDefault();
            return View(RoleViewModel);
        }

        [HttpPost]
        public IActionResult RoleManage(RoleManageViewModel roleManagmentViewModel)
        {

            string oldRole = _userManager.GetRolesAsync(_unitOfWork.ApplicationUser.Get(u => u.Id == roleManagmentViewModel.ApplicationUser.Id)).GetAwaiter().GetResult().FirstOrDefault();

            ApplicationUser applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == roleManagmentViewModel.ApplicationUser.Id);


            if (!(roleManagmentViewModel.ApplicationUser.Role == oldRole))
            {
                //a role was updated
                if (roleManagmentViewModel.ApplicationUser.Role == StaticDetail.Role_Company)
                {
                    applicationUser.CompanyId = roleManagmentViewModel.ApplicationUser.CompanyId;
                }
                if (oldRole == StaticDetail.Role_Company)
                {
                    applicationUser.CompanyId = null;
                }
                _unitOfWork.ApplicationUser.Update(applicationUser);
                _unitOfWork.Save();

                _userManager.RemoveFromRoleAsync(applicationUser, oldRole).GetAwaiter().GetResult();
                _userManager.AddToRoleAsync(applicationUser, roleManagmentViewModel.ApplicationUser.Role).GetAwaiter().GetResult();

            }
            else
            {
                if (oldRole == StaticDetail.Role_Company && applicationUser.CompanyId != roleManagmentViewModel.ApplicationUser.CompanyId)
                {
                    applicationUser.CompanyId = roleManagmentViewModel.ApplicationUser.CompanyId;
                    _unitOfWork.ApplicationUser.Update(applicationUser);
                    _unitOfWork.Save();
                }
            }

            return RedirectToAction("Index");
        }



        //API CALL
        //Sử dụng cho GEt --> GET: ../Admin/User/GetAll
        [HttpGet]
        public IActionResult GetAll()
        {
            List<ApplicationUser> objUserList = _unitOfWork.ApplicationUser.GetAll(includeProperties: "Company").ToList();
            foreach (var user in objUserList)
            {
                user.Role = _userManager.GetRolesAsync(user).GetAwaiter().GetResult().FirstOrDefault();
                if (user.Company == null)
                {
                    //nếu user không có dữ liệu company thì gán object có kiểu company (có property Name = "")
                    user.Company = new Company()
                    {
                        Name = ""
                    };
                }
            }

            //new {}: Anonymous types
            //return JSOn
            return Json(new { data = objUserList });
        }

        [HttpPost]

        //Sử dụng cho POST --> POST: ../Admin/User/LockUnlock
        //Method dùng để khóa tài khoản user
        //[FromBody]: liên kết tham số từ phần thân của yêu cầu HTTP. [FromBody] để chỉ định rằng dữ liệu cho tham số này sẽ được liên kết từ phần thân của yêu cầu.
        //Tham số id của method LockUnlock chính là dữ liệu sẽ được liên kết từ phần thân của yêu cầu, chính là data: JSON.stringify(id) từ function LockUnlock của file user.js
        public IActionResult LockUnlock([FromBody] string id)
        {
            var objFromDb = _unitOfWork.ApplicationUser.Get(u => u.Id == id);
            if (objFromDb == null)
            {
                return Json(new { success = false, message = "Error while Locking/Unlocking" });
            }

            if (objFromDb.LockoutEnd != null && objFromDb.LockoutEnd > DateTime.Now)
            {
                //user is currently locked and we need to unlock them
                objFromDb.LockoutEnd = DateTime.Now;
            }
            else
            {
                objFromDb.LockoutEnd = DateTime.Now.AddYears(1000);
            }
            _unitOfWork.ApplicationUser.Update(objFromDb);
            _unitOfWork.Save();
            return Json(new { success = true, message = "Operation Successful" });
        }
    }
}

