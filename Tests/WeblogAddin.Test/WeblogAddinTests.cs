﻿using System;
using System.Collections.Generic;
using System.Linq;
using WebLogAddin.MetaWebLogApi;
using MarkdownMonster;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using WeblogAddin;

namespace WeblogAddin.Test
{
    [TestClass]
    public class WeblogAddinTests
    {
        private const string ConstWeblogName = "rk7wof4b";
        private const string ConstWordPressWeblogName = "Rick Strahl WordPress";


        public WeblogAddinTests()
        {
            
        }
		



        [TestMethod]
        public void GetPostConfigFromMarkdown()
        {
            WeblogInfo weblogInfo = WeblogAddinConfiguration.Current.Weblogs[ConstWeblogName];
            Post post = new Post() {};

            string markdown = MarkdownWithoutPostId;
            
            var addin = new WeblogAddin.WebLogAddin();
            var meta = WeblogPostMetadata.GetPostConfigFromMarkdown(markdown,post,weblogInfo);

            Console.WriteLine("meta: \r\n" + JsonConvert.SerializeObject(meta, Formatting.Indented));

            Console.WriteLine("post: \r\n" +JsonConvert.SerializeObject(post, Formatting.Indented));

            Assert.IsTrue(meta.Abstract == "Abstract");
            Assert.IsTrue(meta.Keywords == "Keywords");
            Assert.IsTrue(meta.WeblogName == "WebLogName");
        }

        

        [TestMethod]
        public void RawPostMetaWeblogTest()
        {
            
            var rawPost = new Post()
            {
                Body = "<b>Markdown Text</b>",                 
                DateCreated = DateTime.UtcNow,
                mt_keywords = "Test,NewPost",
                CustomFields = new CustomField[]
                {
                    new CustomField()
                    {
                        Id = Guid.NewGuid().ToString(),
                        Key = "mt_Markdown",
                        Value = "**Markdown Text**"
                    }
                },
                PostId = 0,
                Title = "Testing a post"
            };

            WeblogInfo weblogInfo = WeblogAddinConfiguration.Current.Weblogs[ConstWeblogName];

            var wrapper = new MetaWeblogWrapper(weblogInfo.ApiUrl,
                weblogInfo.Username,
                weblogInfo.Password);

             rawPost.PostId = wrapper.NewPost(rawPost, true);            
        }

        [TestMethod]
        
        public void RawPostWordPressTest()
        {
            var rawPost = new Post()
            {
                Body = "<b>Markdown Text</b>",
                DateCreated = DateTime.UtcNow,
                mt_keywords = "Test,NewPost",
                CustomFields = new CustomField[]
                {
                    new CustomField()
                    {
                        Id = Guid.NewGuid().ToString(),
                        Key = "mt_Markdown",
                        Value = "**Markdown Text**"
                    }
                },
                PostId = 0,
                Title = "Testing a post"
            };

            WeblogInfo weblogInfo = WeblogAddinConfiguration.Current.Weblogs[ConstWordPressWeblogName];

            var wrapper = new WordPressWrapper(weblogInfo.ApiUrl,
                weblogInfo.Username,
                weblogInfo.Password);

            rawPost.PostId = wrapper.NewPost(rawPost, true);
        }

        [TestMethod]
        public void GetCategories()
        {
            WeblogInfo weblogInfo = WeblogAddinConfiguration.Current.Weblogs[ConstWeblogName];

            var wrapper = new MetaWeblogWrapper(weblogInfo.ApiUrl,
                weblogInfo.Username,
                weblogInfo.Password);

            var categoryStrings = new List<string>();

            var categories = wrapper.GetCategories();
            foreach (var cat in categories)
            {
                categoryStrings.Add(cat.Description);
            }

            Assert.IsTrue(categoryStrings.Count > 0);

            foreach(string cat in categoryStrings)
                Console.WriteLine(cat);

        }

        [TestMethod]
        public void GetRecentPosts()
        {
            WeblogInfo weblogInfo = WeblogAddinConfiguration.Current.Weblogs[ConstWeblogName];

            var wrapper = new MetaWeblogWrapper(weblogInfo.ApiUrl,
                weblogInfo.Username,
                weblogInfo.Password);

            var posts = wrapper.GetRecentPosts(2).ToList();

            Assert.IsTrue(posts.Count == 2);

            foreach (var post in posts)
                Console.WriteLine(post.Title + "  " + post.DateCreated);
        }

        [TestMethod]
        public void GetRecentPost()
        {
            WeblogInfo weblogInfo = WeblogAddinConfiguration.Current.Weblogs[ConstWeblogName];

            var wrapper = new MetaWeblogWrapper(weblogInfo.ApiUrl,
                weblogInfo.Username,
                weblogInfo.Password);

            var posts = wrapper.GetRecentPosts(2).ToList();

            Assert.IsTrue(posts.Count == 2);

            var postId = posts[0].PostId;

            var post = wrapper.GetPost(postId.ToString());

            Assert.IsNotNull(post);
            Console.WriteLine(post.Title);

            // markdown
            Console.WriteLine(post.CustomFields?[0].Value);
        }



        string MarkdownWithoutPostId = @"### Summary

The time to look at moving your non-secure sites is now, before it's a do or die scenario like mine was. The all SSL based Web is coming without a doubt and it's time to start getting ready for it now.


<!-- Post Configuration -->
<!---
```xml
<abstract>
Abstract
</abstract>
<categories>
Categories
</categories>
<keywords>
Keywords
</keywords>
<weblog>
WebLogName
</weblog>
```
-->
<!-- End Post Configuration -->
";
    }
}