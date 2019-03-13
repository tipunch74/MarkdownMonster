﻿using System;
using CookComputing.XmlRpc;

namespace WebLogAddin.MetaWebLogApi
{
	/// <summary> 
	/// This struct represents information about a user's blog. 
	/// </summary> 
	[XmlRpcMissingMapping(MappingAction.Ignore)]
    public struct XmlRpcUserBlog
	{
		public bool isAdmin;
		public string url;
		public object blogid;
		public string blogName;
		public string xmlrpc;
	}
}