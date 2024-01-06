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
    [Area("Admin")] //Area attribute: tell ProductController belongs to area Admin 
    [Authorize(Roles = StaticDetail.Role_Admin)] //Chỉ có Admin mới có thể thực hiện các chức năng của Product. Áp dụng cho toàn bộ property và method class ProductController
    //Có thể sử dụng [Authorize] cho từng method và property
    public class ProductController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        //IWebHostEnvironment interface là 1 interface có sẵn trong asp.net core mvc -- dùng để lưu trữ file (ảnh, ...)
        //IWebHostEnvironment sẽ được DI(Dependency Injection) implementation
        //sử dụng webHostEnvironment sẽ truy cập được thư mục wwwroot 
        private readonly IWebHostEnvironment _webHostEnvironment; 

        //Asking DI(Dependency Injection) to provide an implementation of the IUnitOfWork
        public ProductController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment)
        {
            //Biến _unitOfWork được gán instance của class UnitOfWork (chính là biến unitOfWork), trong hàm constructor
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
        }

        //Sử dụng cho View - GET
        public IActionResult Index()
        {
            //includeProperties:"Category" --> ghi rõ ràng việc truyền tham số 
            //includeProperties: "Category" --> Get infomation record Category base on FK CategoryID in table Product
            List<Product> objProductList = _unitOfWork.Product.GetAll(includeProperties:"Category").ToList(); //includeProperties:"Category" --> Category chính là property Category trong model Product
            return View(objProductList);
        }
        

        //GET
        //Sử dụng cho View Upsert (Kết hợp View của Update và Insert) - GET
        //int? id: if it Create, it will not have id. But if it Update, it will have id
        public IActionResult Upsert(int? id)
        {
            //_unitOfWork.Category.GetAll() sẽ return IEnumerable<Category>: danh sách các Category
            //Dùng LINQ Select --> .Select(u => new SelectListItem {...}) sẽ return IEnumerable<SelectListItem> vì u => new SelectListItem {...}. LINQ Select giống Map bên JS
            IEnumerable<SelectListItem> CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
            {
                Text = u.Name, //Hiển thị trong các lựa chọn của Dropdown List
                Value = u.Id.ToString() //Giá trị được lưu khi chọn
            });

            //Sử dụng Object Creator để tạo Instance có kiểu ProductViewModel
            //Cách 1:
            ProductViewModel productViewModel = new ProductViewModel()
            {
                CategoryList = CategoryList,
                Product = new Product(),
            };
            //Cách 2: tương đương cách 2
            //Lưu ý: CategoryList vế trái là proprty trong class ProductViewModel, còn CategoryList vế trái chính là biến CategoryList
            //ProductViewModel productViewModel = new ProductViewModel();
            //productViewModel.CategoryList = CategoryList; // 
            //productViewModel.Product = new Product();

            if (id == 0 || id == null)
            {
                //View CREATE
                return View(productViewModel);
            }
            else
            {
                //View Update
                productViewModel.Product = _unitOfWork.Product.Get(u => u.Id == id, includeProperties:"ProductImages"); //ProductImages: chính là property ProductImages trong model Product
                return View(productViewModel);
            }
        }

        //Sử dụng cho POST Upsert - Kết hợp giữa thêm hoặc chỉnh sửa dữ liệu trong bảng Product
        //IFormFile? file: sẽ được nhận dữ liệu từ thẻ <input type="file" name="file"...> trong thẻ form có attrubite enctype="multipart/form-data" từ thư mục Admin/Product/View/Upsert.cshtml
        //Lưu ý: thuộc tính name="file" trong thẻ phải giống với tên biến IFormFile? file. Như vậy biến file mới nhận được dữ liệu
        [HttpPost]
        public IActionResult Upsert(ProductViewModel productViewModel, List<IFormFile> files)
        {
            if (ModelState.IsValid)
            {
                if (productViewModel.Product.Id == 0)
                {
                    //Create
                    _unitOfWork.Product.Add(productViewModel.Product);
                }
                else
                {
                    //Update
                    _unitOfWork.Product.Update(productViewModel.Product);

                }
                //Nếu là create thì sau đoạn Save, biến productViewModel sẽ có id bằng chính id của product vừa mới tạo
                _unitOfWork.Save();


                //_webHostEnvironment.WebRootPath: sẽ return đường dẫn (path) của thư mục wwwroot
                string wwwRootPath = _webHostEnvironment.WebRootPath;
                if(files != null)
                {
                    foreach(IFormFile file in files)
                    {
                        //Guid.NewGuid(): return random name
                        //Path.GetExtension: lấy đuôi file
                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                        string productPath = @"images\products\product-" + productViewModel.Product.Id;
                        //class Path chứa có method làm việc với đường dẫn (path)
                        //biến finalPath sẽ lưu đường dẫn (path) của thư mục product-[id] trong thư mục wwwroot\images\products\
                        string finalPath = Path.Combine(wwwRootPath, productPath);

                        //Directory.Exists(finalPath): Check thư mục (theo path) đó có tồn tại hay chưa, nếu chưa thì tạo mới file đó
                        if (!Directory.Exists(finalPath))
                        {
                            Directory.CreateDirectory(finalPath);
                        }

                        //Thêm ảnh vào đường dẫn finalPath (path)
                        using (var fileStream = new FileStream(Path.Combine(finalPath, fileName), FileMode.Create))
                        {
                            file.CopyTo(fileStream);
                        }

                        ProductImage productImage = new ProductImage
                        {
                            ImageUrl = @"\" + productPath + @"\" + fileName,
                            ProductId = productViewModel.Product.Id,
                        };

                        //Nếu productViewModel.Product.ProductImages = null thì gán cho bằng object rỗng kiểu <ProductImage>
                        if (productViewModel.Product.ProductImages == null)
                        {
                            productViewModel.Product.ProductImages = new List<ProductImage>();
                        }

                        productViewModel.Product.ProductImages.Add(productImage); //method Add của collection List
                    }
                    
                    _unitOfWork.Product.Update(productViewModel.Product);
                    _unitOfWork.Save();

                }             
               
                TempData["success"] = "Product created successfully!.";

                //Chuyển hướng đến action Index của Controller Product
                return RedirectToAction("Index", "Product");
            }
            else
            {
                productViewModel.CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                });
                return View(productViewModel);
            }
        }

        public IActionResult DeleteImage(int imageId)
        {
            var imageToBeDeleted = _unitOfWork.ProductImage.Get(u=>u.Id== imageId);
            var productId = imageToBeDeleted.Id;
            if(imageToBeDeleted != null)
            {
                if (!string.IsNullOrEmpty(imageToBeDeleted.ImageUrl))
                {
                    var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, imageToBeDeleted.ImageUrl.TrimStart('\\'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }

                _unitOfWork.ProductImage.Remove(imageToBeDeleted);
                _unitOfWork.Save();

                TempData["success"] = "Delete Image successfully!.";

            }

            //Chuyển hướng đến action Upsert của Controller Product, và truyền id = productId
            //Cách 1:
            //return RedirectToAction(nameof(Upsert), new{ id = productId });
            //Cách 2:
            return RedirectToAction("Upsert", "Product", new
            {
                id = productId
            }); 
        }

        //API CALL
        //Sử dụng cho GEt --> GET: ../Admin/product/GetAll
        [HttpGet]
        public IActionResult GetAll()
        {
            List<Product> objProductList = _unitOfWork.Product.GetAll(includeProperties: "Category").ToList();

            //new {}: Anonymous types
            //return JSOn
            return Json(new { data = objProductList }) ;
        }

        [HttpDelete]
        //Sử dụng cho DELETE --> DELETE: ../Admin/Product/Delete
        public IActionResult Delete(int? id)
        {
            var productToBeDeleted = _unitOfWork.Product.Get(u=>u.Id == id);    
            if(productToBeDeleted == null)
            {
                return Json(new { data = new { success = false, messgae = "Error while deleting" } });
            }


            string productPath = @"images\products\product-" + id;

            //_webHostEnvironment.WebRootPath return đường dẫn cho thư mục wwwroot
            //class Path chứa có method làm việc với đường dẫn (path)
            //biến finalPath sẽ lưu đường dẫn (path) của thư mục product trong thư mục wwwroot\images\products
            string finalPath = Path.Combine(_webHostEnvironment.WebRootPath, productPath);

            //Directory.Exists(finalPath): Check file (theo path) đó có tồn tại hay chưa, nếu tồn tại thì xóa đường dẫn
            if (Directory.Exists(finalPath))
            {
                //Directory.GetFiles(finalPath): lấy tất các các file trong thư mục theo đường dẫn (path) finalPath
                string[] filePaths = Directory.GetFiles(finalPath); 
                foreach(string filePath in filePaths)
                {
                    //Xóa File
                    System.IO.File.Delete(filePath);
                }

                //Delete thư mục theo đường dẫn (path)
                Directory.Delete(finalPath);
            }




            _unitOfWork.Product.Remove(productToBeDeleted);
            _unitOfWork.Save();
            //new {}: Anonymous types
            //return JSOn
            return Json(new { data = new { success = true, messgae = "Delete Successfully!." } });
        }
    }
}

