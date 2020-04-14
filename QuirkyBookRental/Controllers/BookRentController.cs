﻿using Microsoft.AspNet.Identity;
using QuirkyBookRental.Models;
using QuirkyBookRental.Utility;
using QuirkyBookRental.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using PagedList;

namespace QuirkyBookRental.Controllers
{
    [Authorize]
    public class BookRentController : Controller
    {
        private ApplicationDbContext db;
        public BookRentController()
        {
            db = ApplicationDbContext.Create();
        }

        // GET BookRent
        public ActionResult Index(int? pageNumber, string option = null, string search=null)
        {
            string userid = User.Identity.GetUserId();

            var model = from br in db.BookRental
                        join b in db.Books on br.BookId equals b.Id
                        join u in db.Users on br.UserId equals u.Id

                        select new BookRentalViewModel
                        {
                            BookId = b.Id,
                            RentalPrice = br.RentalPrice,
                            Price = b.Price,
                            FirstName = u.FirstName,
                            LastName = u.LastName,
                            BirthDate = u.BirthDate,
                            ScheduledEndDate = br.ScheduledEndDate,
                            Author = b.Author,
                            Availability = b.Availability,
                            DateAdded = b.DateAdded,
                            Description = b.Description,
                            Email = u.Email,
                            GenreId = b.GenreId,
                            Genre = db.Genres.Where(g => g.Id.Equals(b.GenreId)).FirstOrDefault(),
                            ISBN = b.ISBN,
                            ImageUrl = b.ImageUrl,
                            ProductDimensions = b.ProductDimensions,
                            PublicationDate = b.PublicationDate,
                            Publisher = b.Publisher,
                            RentalDuration = br.RentalDuration,
                            Status = br.Status.ToString(),
                            Title = b.Title,
                            UserId = u.Id,
                            Id = br.Id,
                            StartDate = br.StartDate
                        };


            if(option == "email" && search.Length >0)
            {
                model = model.Where(u => u.Email.Contains(search));
            }
            if (option == "name" && search.Length > 0)
            {
                model = model.Where(u => u.FirstName.Contains(search) || u.LastName.Contains(search));
            }
            if (option == "status" && search.Length > 0)
            {
                model = model.Where(u => u.Status.Contains(search));
            }


            if (!User.IsInRole(SD.AdminUserRole))
            {
                model = model.Where(u => u.UserId.Equals(userid));
            }

            return View(model.ToList().ToPagedList(pageNumber?? 1,5));
        }

        // GET Method
        public ActionResult Create(string title = null, string ISBN = null)
        {
            if (title != null && ISBN != null)
            {
                BookRentalViewModel model = new BookRentalViewModel()
                {
                    Title = title,
                    ISBN = ISBN,
                };
                return View(model);
            }


            return View(new BookRentalViewModel());
        }

        //POST Action method
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(BookRentalViewModel bookRent)
        {
            if (ModelState.IsValid)
            {
                var email = bookRent.Email;

                var userDetails = from u in db.Users
                                  where u.Email.Equals(email)
                                  select new { u.Id, u.FirstName, u.LastName, u.BirthDate };

                var ISBN = bookRent.ISBN;

                Book bookSelected = db.Books.Where(b => b.ISBN == ISBN).FirstOrDefault();

                var rentalDuration = bookRent.RentalDuration;

                var chargeRate = from u in db.Users
                                 join m in db.MembershipTypes on u.MembershipTypeId equals m.Id
                                 where u.Email.Equals(email)
                                 select new { m.ChargeRateOneMonth, m.ChargeRateSixMonth };

                var oneMonthRental = Convert.ToDouble(bookSelected.Price) * Convert.ToDouble(chargeRate.ToList()[0].ChargeRateOneMonth) / 100;
                var sixMonthRental = Convert.ToDouble(bookSelected.Price) * Convert.ToDouble(chargeRate.ToList()[0].ChargeRateSixMonth) / 100;

                double rentalPr = 0;

                if (bookRent.RentalDuration == Int32.Parse(SD.SixMonthCount))
                {
                    rentalPr = sixMonthRental;
                }
                else
                {
                    rentalPr = oneMonthRental;
                }

                BookRent modelToAddToDb = new BookRent
                {
                    BookId = bookSelected.Id,
                    RentalPrice = rentalPr,
                    ScheduledEndDate = bookRent.ScheduledEndDate,
                    RentalDuration = bookRent.RentalDuration,
                    Status = BookRent.StatusEnum.Approved,
                    UserId = userDetails.ToList()[0].Id
                };

                bookSelected.Availability -= 1;
                db.BookRental.Add(modelToAddToDb);
                db.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public ActionResult Reserve(BookRentalViewModel book)
        {
            var userid = User.Identity.GetUserId();
            Book bookToRent = db.Books.Find(book.BookId);
            double rentalPr = 0;

            if (userid != null)
            {
                var chargeRate = from u in db.Users
                                 join m in db.MembershipTypes on u.MembershipTypeId equals m.Id
                                 where u.Id.Equals(userid)
                                 select new { m.ChargeRateOneMonth, m.ChargeRateSixMonth };

                if (book.RentalDuration == Int32.Parse(SD.SixMonthCount))
                {
                    rentalPr = Convert.ToDouble(bookToRent.Price) * Convert.ToDouble(chargeRate.ToList()[0].ChargeRateSixMonth) / 100;
                }
                else
                {
                    rentalPr = Convert.ToDouble(bookToRent.Price) * Convert.ToDouble(chargeRate.ToList()[0].ChargeRateOneMonth) / 100;
                }

                BookRent bookRent = new BookRent
                {
                    BookId = bookToRent.Id,
                    UserId = userid,
                    RentalDuration = book.RentalDuration,
                    RentalPrice = rentalPr,
                    Status = BookRent.StatusEnum.Requested,
                };

                db.BookRental.Add(bookRent);

                var bookInDb = db.Books.SingleOrDefault(c=>c.Id==book.BookId);
                bookInDb.Availability -= 1;
                db.SaveChanges();

                return RedirectToAction("Index", "BookRent");
            }

            return View();
        }

        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            BookRent bookRent = db.BookRental.Find(id);

            var model = getVMFromBookRent(bookRent);

            if (model == null)
            {
                return HttpNotFound();
            }

            return View(model);
        }

        //Decline GET
        public ActionResult Decline(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            BookRent bookRent = db.BookRental.Find(id);

            var model = getVMFromBookRent(bookRent);

            if (model == null)
            {
                return HttpNotFound();
            }

            return View(model);
        }

        //Decline POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Decline(BookRentalViewModel model)
        {
            if (model.Id == 0)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            BookRent bookRent = db.BookRental.Find(model.Id);
            bookRent.Status = BookRent.StatusEnum.Rejected;

            Book bookInDb = db.Books.Find(bookRent.BookId);
            bookInDb.Availability += 1;

            db.SaveChanges();

            return RedirectToAction("Index");
        }

        //Approve GET
        public ActionResult Approve(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            BookRent bookRent = db.BookRental.Find(id);

            var model = getVMFromBookRent(bookRent);

            if (model == null)
            {
                return HttpNotFound();
            }

            return View("Approve", model);
        }

        //Approve POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(BookRentalViewModel model)
        {
            if (model.Id == 0)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            BookRent bookRent = db.BookRental.Find(model.Id);
            bookRent.Status = BookRent.StatusEnum.Approved;

            db.SaveChanges();

            return RedirectToAction("Index");
        }

        //PickUp GET
        public ActionResult PickUp(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            BookRent bookRent = db.BookRental.Find(id);

            var model = getVMFromBookRent(bookRent);

            if (model == null)
            {
                return HttpNotFound();
            }

            return View("Approve", model);
        }

        //PickUp POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PickUp(BookRentalViewModel model)
        {
            if (model.Id == 0)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            BookRent bookRent = db.BookRental.Find(model.Id);
            bookRent.Status = BookRent.StatusEnum.Rented;
            bookRent.StartDate = DateTime.Now;

            if(bookRent.RentalDuration == Int32.Parse(SD.SixMonthCount))
            {
                bookRent.ScheduledEndDate = DateTime.Now.AddMonths(Convert.ToInt32(SD.SixMonthCount));
            }
            else
            {
                bookRent.ScheduledEndDate = DateTime.Now.AddMonths(Convert.ToInt32(SD.OneMonthCount));
            }
            
            db.SaveChanges();

            return RedirectToAction("Index");
        }

        //Return GET
        public ActionResult Return(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            BookRent bookRent = db.BookRental.Find(id);

            var model = getVMFromBookRent(bookRent);

            if (model == null)
            {
                return HttpNotFound();
            }

            return View("Approve", model);
        }

        //Return POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Return(BookRentalViewModel model)
        {
            if (model.Id == 0)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            BookRent bookRent = db.BookRental.Find(model.Id);
            bookRent.Status = BookRent.StatusEnum.Closed;
            bookRent.AdditionalCharge = model.AdditionalCharge;

            Book bookInDb = db.Books.Find(bookRent.BookId);
            bookInDb.Availability += 1;

            bookRent.ActualEndDate = DateTime.Now;

            db.SaveChanges();

            return RedirectToAction("Index");
        }

        //Delete GET
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            BookRent bookRent = db.BookRental.Find(id);

            var model = getVMFromBookRent(bookRent);

            if (model == null)
            {
                return HttpNotFound();
            }

            return View(model);
        }

        //Delete POST
        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int Id)
        {
            if (Id == 0)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            BookRent bookRent = db.BookRental.Find(Id);

            Book bookInDb = db.Books.Where(b => b.Id.Equals(bookRent.BookId)).FirstOrDefault();
            if (!bookRent.Status.ToString().Equals("Rented"))
            {
                bookInDb.Availability += 1;
            }

            db.BookRental.Remove(bookRent);
            db.SaveChanges();

            return RedirectToAction("Index");
        }

        private BookRentalViewModel getVMFromBookRent(BookRent bookRent)
        {
            Book bookSelected = db.Books.Where(b => b.Id == bookRent.BookId).FirstOrDefault();

            var userDetails = from u in db.Users
                              where u.Id.Equals(bookRent.UserId)
                              select new { u.Id, u.FirstName, u.LastName, u.BirthDate, u.Email };

            BookRentalViewModel model = new BookRentalViewModel
            {
                Id = bookRent.Id,
                BookId = bookSelected.Id,
                RentalPrice = bookSelected.Price,
                FirstName = userDetails.ToList()[0].FirstName,
                LastName = userDetails.ToList()[0].LastName,
                BirthDate = userDetails.ToList()[0].BirthDate,
                Email = userDetails.ToList()[0].Email,
                UserId = userDetails.ToList()[0].Id,
                ScheduledEndDate=bookRent.ScheduledEndDate,
                Author=bookSelected.Author,
                StartDate=bookRent.StartDate,
                Availability=bookSelected.Availability,
                DateAdded=bookSelected.DateAdded,
                Description=bookSelected.Description,
                GenreId=bookSelected.GenreId,
                Genre=db.Genres.FirstOrDefault(g=>g.Id.Equals(bookSelected.GenreId)),
                ISBN=bookSelected.ISBN,
                ImageUrl=bookSelected.ImageUrl,
                ProductDimensions=bookSelected.ProductDimensions,
                PublicationDate=bookSelected.PublicationDate,
                Publisher=bookSelected.Publisher,
                RentalDuration=bookRent.RentalDuration,
                Status=bookRent.Status.ToString(),
                Title=bookSelected.Title,
                AdditionalCharge=bookRent.AdditionalCharge
            };

            return model;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
        }
    }
}