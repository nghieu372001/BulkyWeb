using Bulky.DataAccess.Data;
using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models.Models;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BulkyWeb.Areas.Admin.Controllers //namespace = name project (BulkyWeb) + name location folder (Areas/Admin/Controllers)
{
    [Area("Admin")] //Area attribute: tell CategoryController belongs to area Admin 
    [Authorize(Roles = StaticDetail.Role_Admin)] //Chỉ có Admin mới có thể thực hiện các chức năng của Category
    public class CategoryController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        //Asking DI(Dependency Injection) to provide an implementation of the IUnitOfWork
        public CategoryController(IUnitOfWork unitOfWork)
        {
            //Biến _unitOfWork được gán instance của class UnitOfWork (chính là biến unitOfWork), trong hàm constructor
            _unitOfWork = unitOfWork;
        }

        //Sử dụng cho View - GET
        public IActionResult Index()
        {
            List<Category> objCategoryList = _unitOfWork.Category.GetAll().ToList();
            return View(objCategoryList);
        }

        //Sử dụng cho View Create - GET
        public IActionResult Create()
        {
            //Return object rỗng cho View ../Area/Admin/Views/Categoty/Index.cshtml
            return View();
        }

        //Sử dụng cho POST - Thêm dữ liệu vào bảng Category
        [HttpPost]
        public IActionResult Create(Category obj)
        {
            if (obj.Name == obj.DisplayOrder.ToString())
            {
                ModelState.AddModelError("Name", "The DisplayOrder Cannot Exactly Math The Name");
            }
            if (ModelState.IsValid)
            {
                _unitOfWork.Category.Add(obj);
                _unitOfWork.Save();
                TempData["success"] = "Category created successfully!.";

                //Chuyển hướng đến action của Category --> ...Admin/Category/Index --> Area: Admin, Controller: Caterogy, Action: Index 
                return RedirectToAction("Index", "Category");
            }
            return View(); //return view itself
        }

        //Sử dụng cho View Edit - GET
        public IActionResult Edit(int? id)
        {
            if (id is null || id == 0)
            {
                return NotFound();
            }
            Category? categoryFromDb = _unitOfWork.Category.Get(u => u.Id == id);

            if (categoryFromDb == null)
            {
                return NotFound();
            }
            return View(categoryFromDb); // truyền dữ liệu vào View (thư mục: Admin/Views/Category/Edit.cshtml)
        }

        //Sử dụng cho POST - Sửa dữ liệu vào bảng Category
        [HttpPost]
        public IActionResult Edit(Category obj)
        {
            //obj là dữ liệu được POST từ view
            if (ModelState.IsValid)
            {
                _unitOfWork.Category.Update(obj);
                _unitOfWork.Save();
                TempData["success"] = "Category updated successfully!.";

                //Chuyển hướng đến action của Category --> ...Admin/Category/Index --> Area: Admin, Controller: Caterogy, Action: Index 
                return RedirectToAction("Index", "Category");
            }
            return View();//return view itself.
        }


        //Sử dụng cho View Delete - GET
        public IActionResult Delete(int? id)
        {
            if (id is null || id == 0)
            {
                return NotFound();
            }
            Category? categoryFromDb = _unitOfWork.Category.Get(u => u.Id == id);
            if (categoryFromDb == null)
            {
                return NotFound();
            }
            return View(categoryFromDb); // truyền dữ liệu vào View (thư mục: Admin/Views/Category/Edit.cshtml)
        }

        //Sử dụng cho POST - Delete dữ liệu vào bảng Category
        //ActionName("Delete"): Khi POST: ../Category/Delete --> POST với URL (../Category/Delete) này sẽ vào method DeletePOST
        [HttpPost, ActionName("Delete")]
        public IActionResult DeletePOST(int? id)
        {
            Category obj = _unitOfWork.Category.Get(u => u.Id == id);
            if (obj == null)
            {
                return NotFound();
            }
            _unitOfWork.Category.Remove(obj);
            _unitOfWork.Save();
            TempData["success"] = "Category deleted successfully!.";

            //Chuyển hướng đến action của Category --> ...Admin/Category/Index --> Area: Admin, Controller: Caterogy, Action: Index 
            return RedirectToAction("Index", "Category");
        }
    }
}
