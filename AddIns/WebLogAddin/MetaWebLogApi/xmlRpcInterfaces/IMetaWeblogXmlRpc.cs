﻿using CookComputing.XmlRpc;
using WebLogAddin.MetaWebLogApi;

namespace WebLogAddin.MetaWebLogApi.XmlRpcInterfaces
{
    public interface IMetaWeblogXmlRpc : IXmlRpcProxy
    {
        /// <summary>
        /// Deletes the post.
        /// </summary>
        /// <param name="appKey">The app key.</param>
        /// <param name="postid">The postid.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="publish">if set to <c>true</c> [publish].</param>
        /// <returns></returns>
        [XmlRpcMethod("metaWeblog.deletePost")]
        bool DeletePost(string appKey, string postid, string username, string password, bool publish);

        /// <summary>
        /// Gets the categories.
        /// </summary>
        /// <param name="blogid">The blogid.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns></returns>
        [XmlRpcMethod("metaWeblog.getCategories")]
        XmlRpcCategory[] GetCategories(object blogId, string username, string password);

        /// <summary>
        /// Gets the recent posts.
        /// </summary>
        /// <param name="blogid">The blogid.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="numberOfPosts">The number of posts.</param>
        /// <returns></returns>
        [XmlRpcMethod("metaWeblog.getRecentPosts")]
        XmlRpcRecentPost[] GetRecentPosts(object blogId, string username, string password, int numberOfPosts);

        /// <summary>
        /// Gets the user info.
        /// </summary>
        /// <param name="appKey">The app key.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns></returns>
        [XmlRpcMethod("blogger.getUserInfo")]
        XmlRpcUserInfo GetUserInfo(string appKey, string username, string password);

        /// <summary>
        /// News the media object.
        /// </summary>
        /// <param name="blogid">The blogid.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="mediaObject">The media object.</param>
        /// <returns></returns>
        [XmlRpcMethod("metaWeblog.newMediaObject")]
        XmlRpcMediaObjectInfo NewMediaObject(object blogId, string username, string password, XmlRpcMediaObject mediaObject);

        /// <summary>
        /// News the post.
        /// </summary>
        /// <param name="blogid">The blogid.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="content">The content.</param>
        /// <param name="publish">if set to <c>true</c> [publish].</param>
        /// <returns></returns>
        [XmlRpcMethod("metaWeblog.newPost")]
        string NewPost(object blogId, string username, string password, XmlRpcPost content, bool publish);


        [XmlRpcMethod("metaWeblog.editPost")]
        bool EditPost(string postid, string username, string password, XmlRpcPost post, bool publish);

        /// <summary>
        /// Gets the post.
        /// </summary>
        /// <param name="postid">The postid.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns></returns>
        [XmlRpcMethod("metaWeblog.getPost")]
        XmlRpcPost GetPost(object postid, string username, string password);

        /// <summary>
        /// Gets the user blogs.
        /// </summary>
        /// <param name="appKey"></param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns></returns>
        [XmlRpcMethod("blogger.getUsersBlogs")]
        XmlRpcUserBlog[] GetUsersBlogs(string appKey, string username, string password);
    }
}