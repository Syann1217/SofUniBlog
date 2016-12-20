﻿using Blog.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using System.Net;

namespace Blog.Controllers
{
    public class ArticleController : Controller
    {
        // GET: Article
        public ActionResult Index()
        {
            return RedirectToAction("List");
        }

        // Get: Article/List
        public ActionResult List()
        {
            using (var database = new BlogDbContext())
            {
                //Get articles from database
                var articles = database.Articles
                    .Include(a => a.Author)
                    .Include(a => a.Tags)
                    .ToList();

                return View(articles);
            }
        }

        //GET: Article/Details
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (var database = new BlogDbContext())
            {
                //Get the article from database
                var article = database.Articles
                    .Where(a => a.Id == id)
                    .Include(a => a.Author)
                    .Include(a => a.Tags)
                    .First();

                if (article == null)
                {
                    return HttpNotFound();
                }

                article.Views++;

                //save article state in database
                database.Entry(article).State = EntityState.Modified;
                database.SaveChanges();

                return View(article);
            }
        }

        //GET: Article/Create
        [Authorize]
        public ActionResult Create()
        {

            using (var database = new BlogDbContext())
            {
                var model = new ArticleViewModel();
                model.Categories = database.Categories
                    .OrderBy(c => c.Name)
                    .ToList();
                return View(model);
            }
                
        }

        //POST: Article/Create
        [HttpPost]
        [Authorize]
        public ActionResult Create(ArticleViewModel model)
        {
            if (ModelState.IsValid)
            {
                //insert article in DB
                using (var database = new BlogDbContext())
                {
                    //Get author id
                    var authorId = database.Users
                        .Where(u => u.UserName == this.User.Identity.Name)
                        .First()
                        .Id;

                    var article = new Article(authorId, model.Title, model.Content, model.CategoryId);

                    this.SetArticleTags(article, model, database);

                    //Save article in DB
                    database.Articles.Add(article);
                    database.SaveChanges();

                    return RedirectToAction("Index");
                }
            }
            return View(model);
        }

        //GET: Article/Delete
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var database = new BlogDbContext())
            {
                //Get article from database
                var article = database.Articles
                    .Where(a => a.Id == id)
                    .Include(a => a.Author)
                    .Include(a => a.Category)
                    .Include(a => a.Tags)
                    .First();

                //Check if user is authorized to delete
                if (! IsUserAuthorizedToEdit(article))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
                }
                ViewBag.TagsString = string.Join(", ", article.Tags.Select(t => t.Name));

                //Check if article exists
                if (article == null)
                {
                    return HttpNotFound();
                }
                //Pass article to view
                return View(article);
            }
        }

        //POST: Article/Delete
        [HttpPost]
        [ActionName("Delete")]
        public ActionResult DeleteConfirmed(int? id)
        {
            if (id== null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (var database = new BlogDbContext())
            {
                //get article from database
                var article = database.Articles
                    .Where(a => a.Id == id)
                    .Include(a => a.Author)
                    .First();

                //check if article exists
                if(article == null)
                {
                    return HttpNotFound();
                }

                //delete article from database
                database.Articles.Remove(article);
                database.SaveChanges();

                //redirect to index page
                return RedirectToAction("Index");
            }
        }

        //GET: Article/Edit
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (var database = new BlogDbContext())
            {
                //get article from database
                var article = database.Articles
                    .Where(a => a.Id == id)
                    .First();

                //Check if user is authorized to edit
                if (!IsUserAuthorizedToEdit(article))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
                }

                //check if article exists
                if (article == null)
                {
                    return HttpNotFound();
                }
                
                //create the view model
                var model = new ArticleViewModel();
                model.Id = article.Id;
                model.Title = article.Title;
                model.Content = article.Content;
                model.CategoryId = article.CategoryId;
                model.Categories = database.Categories
                    .OrderBy(c => c.Name)
                    .ToList();

                model.Tags = string.Join(", ", article.Tags.Select(t => t.Name));

                //pass the view model to view
                return View(model);
            }
        }

        //POST: Article/Edit
        [HttpPost]
        public ActionResult Edit(ArticleViewModel model)
        {
            //check if model state is valid
            if (ModelState.IsValid)
            {
                using (var database = new BlogDbContext())
                {
                    //get article from database
                    var article = database.Articles
                        .FirstOrDefault(a => a.Id == model.Id);
                    //set article properties
                    article.Title = model.Title;
                    article.Content = model.Content;
                    article.CategoryId = model.CategoryId;
                    this.SetArticleTags(article, model, database);
                    
                    //save article state in database
                    database.Entry(article).State = EntityState.Modified;
                    database.SaveChanges();
                    //redirect to the index page
                    return RedirectToAction("Index");
                }
            }
            //if model state is invalid return the same view
            return View(model);
        }

        private void SetArticleTags(Article article, ArticleViewModel model, BlogDbContext database)
        {
            //Split Tags
            var tagsStrings = model.Tags
                 .Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                 .Select(t => t.ToLower())
                 .Distinct();

            //Clear all current article tags
            article.Tags.Clear();

            //Set new article tags
            foreach (var tagString in tagsStrings)
            {
                //Get tag from the database by its name
                Tag tag = database.Tags.FirstOrDefault(t => t.Name.Equals(tagString));

                //If the tag is null create new tag
                if (tag == null)
                {
                    tag = new Tag() { Name = tagString };
                    database.Tags.Add(tag);
                }

                //Add tags to article tags
                article.Tags.Add(tag);
            }
        }

        private bool IsUserAuthorizedToEdit (Article article)
        {
            bool isAdmin = this.User.IsInRole("Admin");
            bool isAuthor = article.IsAuthor(this.User.Identity.Name);

            return isAdmin || isAuthor;
        }
    }
}
