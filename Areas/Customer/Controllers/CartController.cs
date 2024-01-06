using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Stripe;
using Stripe.Checkout;

namespace BulkyWeb.Areas.Customer.Controllers
{
    [Area("Customer")] //Area attribute: tell CartController belongs to area Customer
    [Authorize] //Don't care what role user. Just care user is login to website or not. That why we will use [Authorize] without any role like [Authorize(Roles = StaticDetail.Role_Admin)]. If user not login, redirect to page login
    //CartController handling payment of customer
    public class CartController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        [BindProperty] //BindProperty là một thuộc tính (attribute) được sử dụng để liên kết (bind) dữ liệu từ các trường của yêu cầu HTTP vào các thuộc tính (property) của biến ShoppingCartViewModel
        public ShoppingCartViewModel ShoppingCartViewModel { get; set; }
        public CartController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public IActionResult Index()
        {
            //Get user ID througth: User object (provide by default) --> User.Identity (Identity is a property) --> Cast type to ClaimsIdentity
            //User.Identity return collection of object contail info about user like id, name, email, ...
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartViewModel = new ShoppingCartViewModel
            {
                //property ShoppingCartList, OrderHeader trong class ShoppingCartViewModel
                //includeProperties:"Product" --> Product chính là property trong class ShoppingCart
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product"),
                OrderHeader = new OrderHeader(),
            };

            //Get All Image Product
            IEnumerable<ProductImage> productImages = _unitOfWork.ProductImage.GetAll();


            //Biến ShoppingCartViewModel là 1 instance của class ShoppingCartViewModel, ShoppingCartList là 1 property của class ShoppingCartViewModel, lưu giá trị  của 1 collection chứa các bản ghi (thỏa mãn điều kiện) của bản ShoppingCarts
            foreach (var cart in ShoppingCartViewModel.ShoppingCartList)
            {
                cart.Product.ProductImages = productImages.Where(u=>u.ProductId == cart.Product.Id).ToList(); //Lưu các image của từng product  
                cart.Price = GetPriceBaseOnQuantity(cart);
                //OrderTotal là property trong class ShoppingCartViewModel
                ShoppingCartViewModel.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }
            return View(ShoppingCartViewModel);
        }

        private double GetPriceBaseOnQuantity(ShoppingCart shoppingCart)
        {
            if (shoppingCart.Count <= 50)
            {
                return shoppingCart.Product.Price;
            }
            else
            {
                if (shoppingCart.Count <= 100)
                {
                    return shoppingCart.Product.Price50;
                }
                else
                {
                    return shoppingCart.Product.Price100;
                }
            }
        }

        public IActionResult Summary()
        {
            //User là một property của lớp ClaimsPrincipal. Lớp ClaimsPrincipal đại diện cho người dùng hiện tại trong hệ thống và chứa thông tin xác thực và quyền của người dùng. 
            //Get user ID througth: User object (provide by default) --> User.Identity (Identity is a property) --> Cast type to ClaimsIdentity
            //User.Identity return collection of object contail info about user like id, name, email, ...
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartViewModel = new ShoppingCartViewModel
            {
                //property ShoppingCartList, OrderHeader trong class ShoppingCartViewModel
                //includeProperties:"Product" --> Product chính là property trong class ShoppingCart
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product"),
                OrderHeader = new OrderHeader(),
            };

            //Get infomation user and save it to ShoppingCartViewModel.OrderHeader.ApplicationUser
            ShoppingCartViewModel.OrderHeader.ApplicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);
            ShoppingCartViewModel.OrderHeader.Name = ShoppingCartViewModel.OrderHeader.ApplicationUser.Name;
            ShoppingCartViewModel.OrderHeader.PhoneNumber = ShoppingCartViewModel.OrderHeader.ApplicationUser.PhoneNumber;
            ShoppingCartViewModel.OrderHeader.StreetAddress = ShoppingCartViewModel.OrderHeader.ApplicationUser.StreetAddress;
            ShoppingCartViewModel.OrderHeader.City = ShoppingCartViewModel.OrderHeader.ApplicationUser.City;
            ShoppingCartViewModel.OrderHeader.State = ShoppingCartViewModel.OrderHeader.ApplicationUser.State;
            ShoppingCartViewModel.OrderHeader.PostalCode = ShoppingCartViewModel.OrderHeader.ApplicationUser.PostalCode;

            //Biến ShoppingCartViewModel là 1 instance của class ShoppingCartViewModel, ShoppingCartList là 1 property của class ShoppingCartViewModel, lưu giá trị  của 1 collection chứa các bản ghi (thỏa mãn điều kiện) của bản ShoppingCarts
            foreach (var cart in ShoppingCartViewModel.ShoppingCartList)
            {
                cart.Price = GetPriceBaseOnQuantity(cart);
                //OrderTotal là property trong class ShoppingCartViewModel
                ShoppingCartViewModel.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }

            //Return to View in folder../Customer/Views/Cart/Summary.cshtml
            return View(ShoppingCartViewModel);
        }

        public IActionResult Plus(int cardId)
        {
            var cartFromDB = _unitOfWork.ShoppingCart.Get(u => u.Id == cardId);
            cartFromDB.Count += 1;

            _unitOfWork.ShoppingCart.Update(cartFromDB);
            _unitOfWork.Save();

            //Chuyển hướng đến action Index của Controller Cart --> Area: Customer, Controller: Cart, Action: Index
            return RedirectToAction("Index", "Cart");
        }

        public IActionResult Minus(int cardId)
        {
            var cartFromDB = _unitOfWork.ShoppingCart.Get(u => u.Id == cardId);
            if (cartFromDB.Count <= 1)
            {
                //remove product from cart
                _unitOfWork.ShoppingCart.Remove(cartFromDB);
                //update session
                HttpContext.Session.SetInt32(StaticDetail.SessionCart, _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == cartFromDB.ApplicationUserId).Count() - 1);
            }
            else
            {
                cartFromDB.Count -= 1;
                _unitOfWork.ShoppingCart.Update(cartFromDB);
            }
            _unitOfWork.Save();

            //Chuyển hướng đến action Index của Controller Cart --> Area: Customer, Controller: Cart, Action: Index
            return RedirectToAction("Index", "Cart");
        }

        public IActionResult Remove(int cardId)
        {
            var cartFromDB = _unitOfWork.ShoppingCart.Get(u => u.Id == cardId); 
            _unitOfWork.ShoppingCart.Remove(cartFromDB);
            //update session
            HttpContext.Session.SetInt32(StaticDetail.SessionCart, _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == cartFromDB.ApplicationUserId).Count() - 1);
            _unitOfWork.Save();

            //Chuyển hướng đến action Index của Controller Cart --> Area: Customer, Controller: Cart, Action: Index
            return RedirectToAction("Index", "Cart");
        }


        [HttpPost]
        [ActionName("Summary")] // --> POST: ../Customer/Cart/Summary --> sẽ lọt vào method này
        public IActionResult SummaryPOST()
        {
            //Get user ID througth: User object (provide by default) --> User.Identity (Identity is a property) --> Cast type to ClaimsIdentity
            //User.Identity return collection of object contail info about user like id, name, email, ...
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            //property ShoppingCartViewModel đã được [BindProperty]
            ShoppingCartViewModel.ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product");
            ShoppingCartViewModel.OrderHeader.OrderDate = System.DateTime.Now;
            ShoppingCartViewModel.OrderHeader.ApplicationUserId = userId;

            //Get infomation user and save it to applicationUser
            ApplicationUser applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);


            //Biến ShoppingCartViewModel là 1 instance của class ShoppingCartViewModel, ShoppingCartList là 1 property của class ShoppingCartViewModel, lưu giá trị  của 1 collection chứa các bản ghi (thỏa mãn điều kiện) của bản ShoppingCarts
            foreach (var cart in ShoppingCartViewModel.ShoppingCartList)
            {
                cart.Price = GetPriceBaseOnQuantity(cart);
                //OrderTotal là property trong class ShoppingCartViewModel
                ShoppingCartViewModel.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }

            //GetValueOrDefault: nếu không có giá trị, nó sẽ trả về giá trị mặc định tương ứng của kiểu dữ liệu của CompanyId
            if (applicationUser.CompanyId.GetValueOrDefault() == 0)
            {
                // it is a regular customer
                ShoppingCartViewModel.OrderHeader.PaymentStatus = StaticDetail.PaymentStatusPending;
                ShoppingCartViewModel.OrderHeader.OrderStatus = StaticDetail.StatusPending;
            }
            else
            {
                //it is a company 
                ShoppingCartViewModel.OrderHeader.PaymentStatus = StaticDetail.PaymentStatusDelayedPayment;
                ShoppingCartViewModel.OrderHeader.OrderStatus = StaticDetail.StatusApproved;
            }

            //Add OrderHeader
            _unitOfWork.OrderHeader.Add(ShoppingCartViewModel.OrderHeader);
            _unitOfWork.Save();

            //Add Order Detail
            foreach (var cart in ShoppingCartViewModel.ShoppingCartList)
            {
                OrderDetail orderDetail = new OrderDetail
                {
                    ProductId = cart.ProductId,
                    OrderHeaderId = ShoppingCartViewModel.OrderHeader.Id,
                    Price = cart.Price,
                    Count = cart.Count
                };
                _unitOfWork.OrderDetail.Add(orderDetail);
                _unitOfWork.Save();
            }

            if (applicationUser.CompanyId.GetValueOrDefault() == 0)
            {
                // it is a regular customer account and we need to capture payment
                // STRIPE LOGIC
                //var domain = "https://localhost:7051"; //Cách 1: gán cứng
                var domain = Request.Scheme + "://" + Request.Host.Value + "/"; //Cách 2: Tự động
                var options = new Stripe.Checkout.SessionCreateOptions
                {
                    SuccessUrl = domain + $"/customer/cart/OrderConfirmation?id={ShoppingCartViewModel.OrderHeader.Id}", //nếu success sẽ redirec tới url này
                    CancelUrl = domain + $"/customer/cart/index", //nếu cancel sẽ redirec tới url này
                    //LineItems --> Product Detail
                    LineItems = new List<Stripe.Checkout.SessionLineItemOptions>(),
                    Mode = "payment",
                };

                //ShoppingCartViewModel.ShoppingCartList là 1 collection kiểu IEnumarable chứa các object có kiểu ShoppingCart
                foreach (var item in ShoppingCartViewModel.ShoppingCartList)
                {
                    //class SessionLineItemOptions là class built-in của Stripe
                    var sessionLineItem = new SessionLineItemOptions
                    {
                        //configuration:
                        //PriceData is basically data used to create a new price object
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(item.Price * 100), //$20.50 => 2050
                            Currency="USD",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                //Can give thing like Nale, Image, Description, ...
                                Name=item.Product.Title
                            }              
                        },
                        Quantity = 1
                    };
                    //Add option in LineItems
                    options.LineItems.Add(sessionLineItem);
                }
                var service = new Stripe.Checkout.SessionService();
                //class Session là class built-in của Stripe
                //session contain ID and PaymentIntendId, Url for page payment of Stripe
                Session session = service.Create(options);

                //session.PaymentIntentId will be null at this time (1), and will have value at method OrderConfirmation
                _unitOfWork.OrderHeader.UpdateStripePaymentID(ShoppingCartViewModel.OrderHeader.Id, session.Id, session.PaymentIntentId);
                _unitOfWork.Save();

                Response.Headers.Add("Location", session.Url);
                //Redirecting to a new URL which is provided by Stripte on code above --> Response.Headers.Add("Location", session.Url);
                return new StatusCodeResult(303);
            }

            //Chuyển hướng đến action OrderConfirmation của Controller Cart --> Area: Customer, Controller: Cart, Action: OrderConfirmation
            //new {...} is Anonymous Type
            //RedirectToAction(actionName, controllerName, routeValues). Trong đó routeValues là giá trị được truyền.
            //Trong trường hợp này thì sẽ truyền tham số id = 1 cho method "OrderConfirmation" ở Controller "Cart"
            return RedirectToAction("OrderConfirmation", "Cart", new
            {
                id = ShoppingCartViewModel.OrderHeader.Id
            });
        }

        
        public IActionResult OrderConfirmation(int id)
        {
            OrderHeader orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == id, includeProperties:"ApplicationUser");
            
            //If not status PaymentStatusDelayedPayment
            if (orderHeader.PaymentStatus != StaticDetail.PaymentStatusDelayedPayment)
            {
                //This is an order by customer
                //class Session, SessionService là class built-in của Stripe
                var service = new SessionService();
                Session session = service.Get(orderHeader.SessionId);

                //session.PaymentStatus is status (paid, unpaid, no_payment_required) of stripe, not application
                if (session.PaymentStatus.ToLower() == "paid")
                {
                    //session.PaymentIntentId will have value (2)
                    _unitOfWork.OrderHeader.UpdateStripePaymentID(id, session.Id, session.PaymentIntentId);
                    _unitOfWork.OrderHeader.UpdateStatus(id, StaticDetail.StatusApproved, StaticDetail.PaymentStatusApproved);
                    _unitOfWork.Save();
                }

                //Done paid then clear session
                HttpContext.Session.Clear();
            }

            //Clear Cart After Payment
            List<ShoppingCart> shoopingCarts = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == orderHeader.ApplicationUserId).ToList();

            //RemoveRange expect IEnumrable, but List implement IEnumrable so it work
            _unitOfWork.ShoppingCart.RemoveRange(shoopingCarts);
            _unitOfWork.Save();

            return View(id);
        }

    }
}

