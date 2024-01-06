using Bulky.DataAccess.Data;
using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.IdentityModel.Tokens;
using System.IO;

namespace BulkyWeb.Areas.Admin.Controllers //namespace = name project (BulkyWeb) + name location folder (Areas/Admin/Controllers)
{
    [Area("Admin")] //Area attribute: tell CompanyController belongs to area Admin 
    [Authorize(Roles = StaticDetail.Role_Admin)] //Chỉ có Admin mới có thể thực hiện các chức năng của Company. Áp dụng cho toàn bộ property và method class CompanyController
    //Có thể sử dụng [Authorize] cho từng method và property
    public class CompanyController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        //Asking DI(Dependency Injection) to provide an implementation of the IUnitOfWork
        public CompanyController(IUnitOfWork unitOfWork)
        {
            //Biến _unitOfWork được gán instance của class UnitOfWork (chính là biến unitOfWork), trong hàm constructor
            _unitOfWork = unitOfWork;
        }

        //Sử dụng cho View - GET
        public IActionResult Index()
        {
            //includeProperties:"Category" --> ghi rõ ràng việc truyền tham số 
            //includeProperties: "Category" --> Get infomation record Category base on FK CategoryID in table Prodct
            List<Company> objCompanyList = _unitOfWork.Company.GetAll().ToList();

            //truyền dữ liệu vào View (thư mục: Area/Admin/Views/Company/Index.cshtml)
            return View(objCompanyList);
        }

        //Sử dụng cho View Upsert (Kết hợp View của Update và Insert) - GET
        //int? id: if it Create, it will not have id. But if it Update, it will have id
        public IActionResult Upsert(int? id)
        {
            if (id == 0 || id == null)
            {
                //View CREATE
                //truyền dữ liệu rỗng vào View (thư mục: Area/Admin/Views/Company/Upsert.cshtml)
                return View(new Company());
            }
            else
            {
                //View Update
                //truyền dữ liệu vào View (thư mục: Area/Admin/Views/Company/Upsert.cshtml)
                Company companyObj = _unitOfWork.Company.Get(u => u.Id == id);
                return View(companyObj);
            }
        }

        //Sử dụng cho POST Upsert - Kết hợp giữa thêm hoặc chỉnh sửa dữ liệu trong bảng Company
        [HttpPost]
        public IActionResult Upsert(Company companyObj)
        {
            if (ModelState.IsValid)
            {
                if(companyObj.Id == 0)
                {
                    //Create
                    _unitOfWork.Company.Add(companyObj);
                }
                else
                {
                    //Update
                    _unitOfWork.Company.Update(companyObj);

                }
                _unitOfWork.Save();
                TempData["success"] = "Company created successfully!.";

                //Chuyển hướng đến action Index trong Controller Company (thư mục: Area/Admin/Views/Company/Index.cshtml)
                return RedirectToAction("Index", "Company");
            }
            else
            {
                //Return To View (thư mục: Area/Admin/Views/Company/Upsert.cshtml)
                return View(companyObj);
            }
        }

        //API CALL
        //Sử dụng cho GEt --> GET: ../Admin/Company/GetAll
        [HttpGet]
        public IActionResult GetAll()
        {
            List<Company> objCompanyList = _unitOfWork.Company.GetAll().ToList();

            //new {}: Anonymous types
            //return JSOn
            return Json(new { data = objCompanyList }) ;
        }

        [HttpDelete]
        //Sử dụng cho DELETE --> DELETE: ../Admin/Company/Delete
        public IActionResult Delete(int? id)
        {
            var CompanyToBeDeleted = _unitOfWork.Company.Get(u=>u.Id == id);    
            if(CompanyToBeDeleted == null)
            {
                return Json(new { data = new { success = false, messgae = "Error while deleting" } });
            }
            _unitOfWork.Company.Remove(CompanyToBeDeleted);
            _unitOfWork.Save();
            return Json(new { data = new { success = true, messgae = "Delete Successfully!." } });
        }
    }
}

