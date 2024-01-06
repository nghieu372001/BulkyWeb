using Bulky.DataAccess.Repository;
using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using Stripe.Climate;
using System.Diagnostics;
using System.Security.Claims;

namespace BulkyWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize] //Don't care what role user. Just care user is login to website or not. That why we will use [Authorize] without any role like [Authorize(Roles = StaticDetail.Role_Admin)]. If user not login, redirect to page login
    //class OrderController handling payment of company
    public class OrderController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        [BindProperty] //BindProperty là một thuộc tính (attribute) được sử dụng để liên kết (bind) dữ liệu từ các trường của yêu cầu HTTP vào các thuộc tính (property) của biến OrderViewModel
        public OrderViewModel OrderViewModel { get; set; }

        public OrderController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        //Sử dụng cho View - GET
        public IActionResult Index()
        {
            return View();
        }

        //Sử dụng cho View - GET
        public IActionResult Details(int orderId)
        {
            OrderViewModel = new OrderViewModel
            {
                //OrderHeader,OrderDetails là 2 property trong class  OrderViewModel
                OrderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == orderId, includeProperties: "ApplicationUser"),
                OrderDetails = _unitOfWork.OrderDetail.GetAll(u => u.OrderHeaderId == orderId, includeProperties: "Product")
            };
            return View(OrderViewModel);
        }


        [HttpPost]
        [Authorize(Roles = StaticDetail.Role_Admin + "," + StaticDetail.Role_Employee)] // Chỉ có role admin, employee mới thực hiện được chức năng này
        public IActionResult UpdateOrderDetail()
        {
            //Vì property OrderViewModel được binding nên khi được POST từ view: Area/Admin/Order/Details.cshtm thì dữ liệu (có kiểu OrderViewModel) được post từ view sẽ được lưu vào property OrderViewModel được binding --> Nên mới sử dụng được OrderViewModel.OrderHeader.Id
            //Lấy dữ liệu từ id
            var orderHeaderFromDb = _unitOfWork.OrderHeader.Get(u => u.Id == OrderViewModel.OrderHeader.Id);
            //Update dữ liệu
            orderHeaderFromDb.Name = OrderViewModel.OrderHeader.Name;
            orderHeaderFromDb.PhoneNumber = OrderViewModel.OrderHeader.PhoneNumber;
            orderHeaderFromDb.StreetAddress = OrderViewModel.OrderHeader.StreetAddress;
            orderHeaderFromDb.City = OrderViewModel.OrderHeader.City;
            orderHeaderFromDb.State = OrderViewModel.OrderHeader.State;
            orderHeaderFromDb.PostalCode = OrderViewModel.OrderHeader.PostalCode;
            if (!string.IsNullOrEmpty(OrderViewModel.OrderHeader.Carrier))
            {
                orderHeaderFromDb.Carrier = OrderViewModel.OrderHeader.Carrier;
            }
            if (!string.IsNullOrEmpty(OrderViewModel.OrderHeader.TrackingNumber))
            {
                orderHeaderFromDb.Carrier = OrderViewModel.OrderHeader.TrackingNumber;
            }
            _unitOfWork.OrderHeader.Update(orderHeaderFromDb);
            _unitOfWork.Save();

            TempData["Success"] = "Order Details Updated Successfully.";

            //Chuyển hướng Action -->Area:Admin, Controller: Order, Action: Details
            //RedirectToAction(actionName, controllerName, routeValues). Trong đó routeValues là giá trị được truyền.
            //Trong trường hợp này thì sẽ truyền tham số orderId  cho method "Details" ở Controller "Order"
            return RedirectToAction("Details", "Order", new
            {
                orderId = orderHeaderFromDb.Id
            });
        }


        [HttpPost]
        [Authorize(Roles = StaticDetail.Role_Admin + "," + StaticDetail.Role_Employee)] // Chỉ có role admin, employee mới thực hiện được chức năng này
        public IActionResult StartProcessing()
        {
            //proprerty OrderViewModel của class OrderController được binding 
            _unitOfWork.OrderHeader.UpdateStatus(OrderViewModel.OrderHeader.Id, StaticDetail.StatusInProcess);
            _unitOfWork.Save();

            //Chuyển hướng Action -->Area:Admin, Controller: Order, Action: Details
            //RedirectToAction(actionName, controllerName, routeValues). Trong đó routeValues là giá trị được truyền.
            //Trong trường hợp này thì sẽ truyền tham số orderId  cho method "Details" ở Controller "Order"
            return RedirectToAction("Details", "Order", new
            {
                orderId = OrderViewModel.OrderHeader.Id
            });
        }

        [HttpPost]
        [Authorize(Roles = StaticDetail.Role_Admin + "," + StaticDetail.Role_Employee)] // Chỉ có role admin, employee mới thực hiện được chức năng này
        public IActionResult ShipOrder()
        {
            //proprerty OrderViewModel của class OrderController được binding 
            var orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == OrderViewModel.OrderHeader.Id);
            orderHeader.TrackingNumber = OrderViewModel.OrderHeader.TrackingNumber;
            orderHeader.Carrier = OrderViewModel.OrderHeader.Carrier;
            orderHeader.OrderStatus = StaticDetail.StatusShipped;
            orderHeader.ShippingDate = DateTime.Now;

            if (orderHeader.PaymentStatus == StaticDetail.PaymentStatusDelayedPayment)
            {
                orderHeader.PaymentDueDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30));
            }

            _unitOfWork.OrderHeader.Update(orderHeader);
            _unitOfWork.Save();

            TempData["Success"] = "Order Shipped Successfully.";

            //Chuyển hướng Action -->Area:Admin, Controller: Order, Action: Details
            //RedirectToAction(actionName, controllerName, routeValues). Trong đó routeValues là giá trị được truyền.
            //Trong trường hợp này thì sẽ truyền tham số orderId  cho method "Details" ở Controller "Order"
            return RedirectToAction("Details", "Order", new
            {
                orderId = OrderViewModel.OrderHeader.Id
            });
        }


        [HttpPost]
        [Authorize(Roles = StaticDetail.Role_Admin + "," + StaticDetail.Role_Employee)] // Chỉ có role admin, employee mới thực hiện được chức năng này
        public IActionResult CancelOrder()
        {
            //proprerty OrderViewModel của class OrderController được binding 
            var orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == OrderViewModel.OrderHeader.Id);

            if (orderHeader.PaymentStatus == StaticDetail.PaymentStatusApproved)
            {
                //class RefundCreateOptions thuộc STRIPE
                var options = new RefundCreateOptions
                {
                    Reason = RefundReasons.RequestedByCustomer,
                    PaymentIntent = orderHeader.PaymentIntentId // sử dụng property PaymentIntentId trong class OrderHeader để refund (trả lại) tiền
                };

                var service = new RefundService();
                Refund refund = service.Create(options);

                _unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, StaticDetail.StatusCancelled, StaticDetail.StatusRefunded);
            }
            else
            {
                _unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, StaticDetail.StatusCancelled, StaticDetail.StatusCancelled);
            }
            _unitOfWork.Save();

            TempData["Success"] = "Order Cancelled Successfully.";

            //Chuyển hướng Action -->Area:Admin, Controller: Order, Action: Details
            //RedirectToAction(actionName, controllerName, routeValues). Trong đó routeValues là giá trị được truyền.
            //Trong trường hợp này thì sẽ truyền tham số orderId  cho method "Details" ở Controller "Order"
            return RedirectToAction("Details", "Order", new
            {
                orderId = OrderViewModel.OrderHeader.Id
            });
        }

        [HttpPost]
        [ActionName("Details")] //POST ../Admin/Order/Details --> lọt vào method DetailsPayNow
        public IActionResult DetailsPayNow(int orderId)
        {
            //proprerty OrderViewModel của class OrderController được binding 
            OrderViewModel.OrderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == OrderViewModel.OrderHeader.Id, includeProperties: "ApplicationUser");
            OrderViewModel.OrderDetails = _unitOfWork.OrderDetail.GetAll(u => u.OrderHeaderId == OrderViewModel.OrderHeader.Id, includeProperties: "Product");

            // STRIPE LOGIC
            //var domain = "https://localhost:7051"; //Cách 1: gán cứng
            var domain = Request.Scheme + "://" + Request.Host.Value + "/"; //Cách 2: Tự động
            var options = new Stripe.Checkout.SessionCreateOptions
            {
                SuccessUrl = domain + $"admin/order/PaymentConfirmation?orderHeaderId={OrderViewModel.OrderHeader.Id}", //nếu success sẽ redirec tới url này
                CancelUrl = domain + $"admin/order/details?orderId={OrderViewModel.OrderHeader.Id}", //nếu cancel sẽ redirec tới url này
                //LineItems --> Product Detail
                LineItems = new List<Stripe.Checkout.SessionLineItemOptions>(),
                Mode = "payment",
            };

            //OrderViewModel.OrderDetails là 1 collection kiểu IEnumarable chứa các object có kiểu OrderDetail
            foreach (var item in OrderViewModel.OrderDetails)
            {
                //class SessionLineItemOptions là class built-in của Stripe
                var sessionLineItem = new SessionLineItemOptions
                {
                    //configuration:
                    //PriceData is basically data used to create a new price object
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(item.Price * 100), //$20.50 => 2050
                        Currency = "USD",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            //Can give thing like Nale, Image, Description, ...
                            Name = item.Product.Title
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
            _unitOfWork.OrderHeader.UpdateStripePaymentID(OrderViewModel.OrderHeader.Id, session.Id, session.PaymentIntentId);
            _unitOfWork.Save();

            Response.Headers.Add("Location", session.Url);
            //Redirecting to a new URL which is provided by Stripte on code above --> Response.Headers.Add("Location", session.Url);
            return new StatusCodeResult(303);

        }


        //GET 
        public IActionResult PaymentConfirmation(int orderHeaderId)
        {
            //proprerty OrderViewModel của class OrderController được binding 
            OrderHeader orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == orderHeaderId);

            //If not status PaymentStatusDelayedPayment
            if (orderHeader.PaymentStatus == StaticDetail.PaymentStatusDelayedPayment)
            {
                //This is an order by company
                //class Session, SessionService là class built-in của Stripe
                var service = new SessionService();
                Session session = service.Get(orderHeader.SessionId);

                //session.PaymentStatus is status (paid, unpaid, no_payment_required) of stripe, not application
                if (session.PaymentStatus.ToLower() == "paid")
                {
                    //session.PaymentIntentId will have value (2)
                    _unitOfWork.OrderHeader.UpdateStripePaymentID(OrderViewModel.OrderHeader.Id, session.Id, session.PaymentIntentId);
                    _unitOfWork.OrderHeader.UpdateStatus(orderHeaderId, orderHeader.OrderStatus, StaticDetail.PaymentStatusApproved);
                    _unitOfWork.Save();
                }
            }

            return View(orderHeader);
        }



        //API CALL
        //Sử dụng cho GET --> GET: ../Admin/Order/GetAll
        [HttpGet]
        public IActionResult GetAll(string status)
        {
            IEnumerable<OrderHeader> objOrderHeaders;

            if (User.IsInRole(StaticDetail.Role_Admin) || User.IsInRole(StaticDetail.Role_Employee))
            {
                objOrderHeaders = _unitOfWork.OrderHeader.GetAll(includeProperties: "ApplicationUser").ToList();
            }
            else
            {
                //User là một property của lớp ClaimsPrincipal. Lớp ClaimsPrincipal đại diện cho người dùng hiện tại trong hệ thống và chứa thông tin xác thực và quyền của người dùng.               
                //User.Identity trả ra collection gồm các object chứa các thông tin như name, email, ...
                //Lấy UserID đang loggin
                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
                objOrderHeaders = _unitOfWork.OrderHeader.GetAll(u => u.ApplicationUserId == userId, includeProperties: "ApplicationUser");
            }

            switch (status)
            {
                case "inprocess":
                    objOrderHeaders = objOrderHeaders.Where(u => u.PaymentStatus == StaticDetail.PaymentStatusDelayedPayment);
                    break;
                case "pending":
                    objOrderHeaders = objOrderHeaders.Where(u => u.PaymentStatus == StaticDetail.StatusInProcess);
                    break;
                case "completed":
                    objOrderHeaders = objOrderHeaders.Where(u => u.PaymentStatus == StaticDetail.StatusShipped);
                    break;
                case "approved":
                    objOrderHeaders = objOrderHeaders.Where(u => u.PaymentStatus == StaticDetail.StatusApproved);
                    break;
                case "all":
                    // Not do to something
                    break;
                default:
                    break;
            }

            //new {}: Anonymous types
            //return JSOn
            return Json(new { data = objOrderHeaders });
        }
    }
}
