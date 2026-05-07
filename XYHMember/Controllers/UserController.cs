using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using XYHMember.Context;

namespace XYHMember.Controllers
{
    [AuthFilter]
    public class UserController : Controller
    {
        private XYHDbContext db = new XYHDbContext();

        // GET: User
        public ActionResult Update(int id)
        {

            return View(id);
        }

        public ActionResult UserList()
        {
            var users = db.Users.ToList();
            return View(users);
        }

        public ActionResult AddUser()
        {

            return View();
        }

        //重置默认密码
        [HttpPost]
        public ActionResult UpdatePass(int userId)
        {
            try
            {
                var updatepwd = db.Users.Find(userId);
                if (updatepwd != null)
                {
                    updatepwd.Password = PasswordHelper.Hash("Rich@869");

                    db.Entry(updatepwd).State = EntityState.Modified;

                    int rowsAffected = db.SaveChanges();
                    if (rowsAffected > 0)
                    {
                        // 成功保存了更改
                        Console.WriteLine("保存成功，受影响的行数：" + rowsAffected);
                        //return Json(new { success = true });
                        return Json(new { success = true, redirectTo = Url.Action("Login", "Home") });
                    }
                    else
                    {
                        // 没有更改被保存
                        Console.WriteLine("没有更改被保存到数据库");
                        return Json(new { success = false });
                    }

                }
                else
                {
                    return Json(new { success = false, errorMessage = "User not found." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, errorMessage = ex.Message });
            }
        }

        //查询 根据名字查询
        [HttpGet]
        public ActionResult GetUserByName(string username)
        {
            var user = db.Users.Where(u => u.Username.Contains(username)).ToList();
            if (user != null)
            {
                // 如果找到了用户，将用户信息返回给视图
                return View("UserList", user);
            }
            else
            {
                // 如果未找到用户，重定向到 Index 页面，并显示消息
                TempData["Message"] = $"User with Name {username} not found.";
                return RedirectToAction("UserList", "User");
            }
        }

        //新增
        [HttpPost]
        public ActionResult Add(XYHUserEntity user)
        {
            user.Username = Request["username"];
            user.Password = PasswordHelper.Hash(Request["pwd"]);
            user.JobNumber = Request["jobnumber"];
            user.Name = Request["name"];

            if (ModelState.IsValid)
            {
                db.Users.Add(user);
                db.SaveChanges();
                return Content("<script>window.close(); window.opener.location.reload();</script>");

            }

            return Content("<script>window.close(); window.opener.location.reload();</script>");

        }

        //删除
        [HttpPost]
        public ActionResult DeleteUser(int id)
        {
            var user = db.Users.FirstOrDefault(u => u.UserId == id);
            if (user != null)
            {
                if (user.UserId == 1)
                {
                    return Content("<script>window.parent.location.href = '/Home/Login';</script>");
                }
                db.Users.Remove(user);
                db.SaveChanges();
            }

            return RedirectToAction("UserList", "User");
        }



        //修改密码
        [HttpPost]
        public ActionResult UpdatePwd(int id)
        {
            var updatepwd = db.Users.Find(id);
            if (updatepwd != null)
            {
                updatepwd.Password = PasswordHelper.Hash(Request["password"]);

                db.Entry(updatepwd).State = EntityState.Modified;

                int rowsAffected = db.SaveChanges();
                if (rowsAffected > 0)
                {
                    // 成功保存了更改
                    Console.WriteLine("保存成功，受影响的行数：" + rowsAffected);
                    return Content("<script>window.parent.location.href = '/Home/Login';</script>");

                }
                else
                {
                    // 没有更改被保存
                    return Content("<script>window.parent.location.href = '/User/UserList';</script>");
                }

            }
            else
            {
                return Content("<script>window.parent.location.href = '/User/UserList';</script>");
            }

        }



    }
}