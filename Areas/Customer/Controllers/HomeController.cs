using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models.Models;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;

namespace BulkyWeb.Areas.Customer.Controllers
{
    [Area("Customer")] //Area attribute: tell HomeController belongs to area Customer
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IUnitOfWork _unitOfWork;

        //Asking DI(Dependency Injection) to provide an implementation of the IUnitOfWork
        public HomeController(ILogger<HomeController> logger, IUnitOfWork unitOfWork)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        //GET
        public IActionResult Index()
        {
            //includeProperties: "Category" --> Get infomation record Category base on FK CategoryID in table Product
            //includeProperties: "Category,ProductImages" --> Category,ProductImages là 2 property của model Product
            //Lưu ý:
            //+ Đối với Category vì được khai báo Foreign Key trong Model Product nên khi truy vấn sẽ dùng INNER JOIN theo ID --> SELECT * FROM [Products] AS [p] INNER JOIN [Categories] AS[c] ON [p].[CategoryId] = [c].[Id]
            //+ Đối với ProductImages vì không được khai báo Foreign Key trong Model Product nên khi truy vấn sẽ dùng LEFT JOIN theo ID --> SELECT * FROM [Products] AS [p] INNER JOIN [Categories] AS[c] ON [p].[CategoryId] = [c].[Id] LEFT JOIN [ProductImages] AS [p0] ON [p].[Id] = [p0].[ProductId]
            IEnumerable<Product> ProductList = _unitOfWork.Product.GetAll(includeProperties: "Category,ProductImages") ;
            return View(ProductList);
        }

        //GET
        public IActionResult Details(int productID)
        {
            //Object Initializers
            ShoppingCart cart = new ShoppingCart
            {
                //Property Product, Count, ProductId trong class ShoppingCart
                Product = _unitOfWork.Product.Get(u => u.Id == productID, includeProperties: "Category,ProductImages"),
                Count = 1,
                ProductId = productID
            };
            return View(cart);
        }


        //POST
        [HttpPost]
        [Authorize] //Don't care what role user. Just care user is login to website or not. That why we will use [Authorize] without any role like [Authorize(Roles = StaticDetail.Role_Admin)] 

        public IActionResult Details(ShoppingCart shoppingCart)
        {
            //Get user ID througth: User object (provide by default) --> User.Identity (Identity is a property) --> Cast type to ClaimsIdentity
            //User.Identity return collection of object contail info about user like id, name, email, ...
            var claimsIdentity = (ClaimsIdentity)User.Identity; 
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            //Because variable shoppingCart have type ShoppingCart, so it contain property ApplicationUserId. This property had defined in class ShoppingCart
            shoppingCart.ApplicationUserId = userId;


            //Check ProductId Exists In UserID
            ShoppingCart cartFromDB = _unitOfWork.ShoppingCart.Get(u => u.ApplicationUserId == userId && u.ProductId == shoppingCart.ProductId);
            if(cartFromDB != null)
            {
                //Shopping Cart Exists
                cartFromDB.Count = cartFromDB.Count + shoppingCart.Count; //update count exists in DB
                _unitOfWork.ShoppingCart.Update(cartFromDB);
                _unitOfWork.Save();
            }
            else
            {
                //If not exists Add to DB
                _unitOfWork.ShoppingCart.Add(shoppingCart);
                _unitOfWork.Save();

                //Add SESSION when user add item to cart
                //Đếm số bản ghi của user có trong bảng ShoppingCarts
                var countDistinctProductInCartOfUser = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId).Count();
                HttpContext.Session.SetInt32(StaticDetail.SessionCart, countDistinctProductInCartOfUser);
            }

            TempData["success"] = "Cart updated successfully";


            //Chuyển hướng đến action Index của Controller Home 
            //return RedirectToAction("Index", "Home"); //Cách 1
            return RedirectToAction(nameof(Index));  //Cách 2
        }

        //GET
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
