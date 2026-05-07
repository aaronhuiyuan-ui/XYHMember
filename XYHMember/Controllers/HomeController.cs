
using System.Linq;
using System.Web.Mvc;
using XYHMember.Context;

namespace XYHMember.Controllers
{
    public class HomeController : Controller
    {
        private XYHDbContext _db = new XYHDbContext();

        public ActionResult Login()
        {
            return View();
        }

        [AuthFilter]
        public ActionResult Index(string Username,int UserId)
        {
            ViewBag.Username = Username;
            ViewBag.UserId = UserId;
            return View();
        }

        //登陆失败跳转
        public ActionResult Demo404()  
        {
            return RedirectToAction("Login", "Home");
        }



        [HttpPost]
        public ActionResult Login(XYHUserEntity user)
        {
            user.Username = Request["name"];
            user.Password = Request["password"];
            if (!string.IsNullOrEmpty(user.Username) && !string.IsNullOrEmpty(user.Password))
            {
                var vaild = _db.Users.FirstOrDefault(u => u.Username == user.Username);
                if (vaild != null && PasswordHelper.Verify(user.Password, vaild.Password))
                {
                    // 如果是旧版明文密码，自动升级为哈希
                    if (!vaild.Password.StartsWith("SHA256$"))
                    {
                        vaild.Password = PasswordHelper.Hash(user.Password);
                        _db.SaveChanges();
                    }

                    Session["UserId"] = vaild.UserId;
                    return RedirectToAction("Index", new { user.Username, vaild.UserId });
                }
                else
                {
                    return Content("<script>alert('用户名或密码不正确');location.href='Demo404'</script>");
                }
            }
            else
            {
                return RedirectToAction("Login", "Home");
            }
        }



    }
}