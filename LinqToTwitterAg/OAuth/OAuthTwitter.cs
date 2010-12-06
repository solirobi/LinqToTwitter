﻿/***********************************************************
 * Credits:
 * 
 * Eran Sandler -
 * OAuthBase Class
 * 
 * http://oauth.googlecode.com/svn/code/csharp/
 * 
 * Shannon Whitley -
 * Example of how to use modified version of
 * Eran Sandler's OAuthBase class in C#
 * 
 * http://www.voiceoftech.com/swhitley/?p=681
 * 
 * Joe Mayo -
 * 
 * Modified 5/17/09
 ***********************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;
using System.Net;
using System.IO;
using System.Threading;

#if SILVERLIGHT
    using System.Windows.Browser;
#else
    using System.Web;
#endif

namespace LinqToTwitter
{
    /// <summary>
    /// helps perform OAuth Authorization for LINQ to Twitter
    /// </summary>
    public class OAuthTwitter : OAuthBase, IOAuthTwitter
    {
        #region Properties

        /// <summary>
        /// user agent header sent to Twitter
        /// </summary>
        public string OAuthUserAgent { get; set; }

        /// <summary>
        /// Consumer Key
        /// </summary>
        public string OAuthConsumerKey { get; set; }

        /// <summary>
        /// Consumer Secret
        /// </summary>
        public string OAuthConsumerSecret { get; set; }

        /// <summary>
        /// OAuth Token
        /// </summary>
        public string OAuthToken { get; set; }

        /// <summary>
        /// OAuth Verifier
        /// </summary>
        public string OAuthVerifier { get; set; }

        /// <summary>
        /// OAuth Token Secret
        /// </summary>
        public string OAuthTokenSecret { get; set; }

        #endregion

        /// <summary>
        /// Get the link to Twitter's authorization page for this application.
        /// </summary>
        /// <param name="readOnly">true for read-only, otherwise read/Write</param>
        /// <returns>The url with a valid request token, or a null string.</returns>
        public string AuthorizationLinkGet(string requestToken, string authorizeUrl, string callback, bool readOnly, bool forceLogin)
        {
            string response = oAuthWebRequest(HttpMethod.GET, requestToken, String.Empty, callback);
            return PrepareAuthorizeUrl(authorizeUrl, readOnly, forceLogin, response);
        }

        private string PrepareAuthorizeUrl(string authorizeUrl, bool readOnly, bool forceLogin, string response)
        {
            string authUrl = string.Empty;

            if (response.Length > 0)
            {
                var prefixChar = "?";

                string oAuthToken =
                    (from nameValPair in response.Split('&')
                     let pair = nameValPair.Split('=')
                     where pair[0] == "oauth_token"
                     select pair[1])
                    .SingleOrDefault();

                if (oAuthToken != null)
                {
                    OAuthToken = oAuthToken;
                    authUrl = authorizeUrl + "?oauth_token=" + oAuthToken;
                    prefixChar = "&";
                }

                if (readOnly)
                {
                    authUrl += prefixChar + "oauth_access_type=read";
                    prefixChar = "&";
                }

                if (forceLogin)
                {
                    authUrl += prefixChar + "force_login=true";
                }
            }
            return authUrl;
        }

        /// <summary>
        /// Exchange the request token for an access token.
        /// </summary>
        /// <param name="authToken">The oauth_token is supplied by Twitter's authorization page following the callback.</param>
        public void AccessTokenGet(string authToken, string verifier, string accessTokenUrl, string callback, out string screenName, out string userID)
        {
            this.OAuthToken = authToken;
            this.OAuthVerifier = verifier;
            screenName = string.Empty;
            userID = string.Empty;

            string response = oAuthWebRequest(HttpMethod.GET, accessTokenUrl, String.Empty, callback);

            ProcessAccessTokenResponse(ref screenName, ref userID, response);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="screenName"></param>
        /// <param name="userID"></param>
        /// <param name="response"></param>
        private void ProcessAccessTokenResponse(ref string screenName, ref string userID, string response)
        {
            if (response.Length > 0)
            {
                var qs =
                    (from nameValPair in response.Split('&')
                     let pair = nameValPair.Split('=')
                     select new
                     {
                         Key = pair[0],
                         Value = pair[1]
                     })
                    .ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value);

                if (qs["oauth_token"] != null)
                {
                    this.OAuthToken = qs["oauth_token"];
                }

                if (qs["oauth_token_secret"] != null)
                {
                    this.OAuthTokenSecret = qs["oauth_token_secret"];
                }

                if (qs["screen_name"] != null)
                {
                    screenName = qs["screen_name"];
                }

                if (qs["user_id"] != null)
                {
                    userID = qs["user_id"];
                }
            }
        }

        /// <summary>
        /// returns a query string for an OAuth request
        /// </summary>
        /// <param name="url">Twitter query</param>
        /// <returns>Query string with OAuth parameters</returns>
        public void GetOAuthQueryString(HttpMethod method, string url, string callback, out string outUrl, out string queryString)
        {
            Uri uri = new Uri(url);

            string nonce = this.GenerateNonce();
            string timeStamp = this.GenerateTimeStamp();

            //Generate Signature
            string sig = this.GenerateSignature(uri,
                this.OAuthConsumerKey,
                this.OAuthConsumerSecret,
                this.OAuthToken,
                this.OAuthTokenSecret,
                this.OAuthVerifier,
                callback,
                method.ToString(),
                timeStamp,
                nonce,
                out outUrl,
                out queryString);

            queryString += "&oauth_signature=" + HttpUtility.UrlEncode(sig);
        }

        /// <summary>
        /// processes POST request parameters
        /// </summary>
        /// <param name="url">url of request, without query string</param>
        /// 
        public string GetOAuthQueryStringForPost(string url)
        {
            OAuthVerifier = null;

            string outUrl;
            string queryString;
            GetOAuthQueryString(HttpMethod.POST, url, string.Empty, out outUrl, out queryString);

            const int Key = 0;
            const int Value = 1;

            var headerItems =
                from param in queryString.Split('&')
                let keyValPair = param.Split('=')
                select
                    keyValPair[Key] +
                    "=\"" +
                    keyValPair[Value] +
                    "\"";

            return "OAuth " + string.Join(",", headerItems.ToArray());
        }

        /// <summary>
        /// Url Encodes for OAuth Authentication
        /// </summary>
        /// <param name="value">string to be encoded</param>
        /// <returns>UrlEncoded string</returns>
        public string TwitterParameterUrlEncode(string value)
        {
            string ReservedChars = @"`!@#$%^&*()_-+=.~,:;'?/|\[] ";
            string UnReservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";

            var result = new StringBuilder();

            if (string.IsNullOrEmpty(value))
                return string.Empty;

            foreach (var symbol in value)
            {
                if (UnReservedChars.IndexOf(symbol) != -1)
                {
                    result.Append(symbol);
                }
                else if (ReservedChars.IndexOf(symbol) != -1)
                {
                    result.Append('%' + String.Format("{0:X2}", (int)symbol).ToUpper());
                }
                else
                {
                    var encoded = HttpUtility.UrlEncode(symbol.ToString()).ToUpper();

                    if (!string.IsNullOrEmpty(encoded))
                    {
                        result.Append(encoded);
                    }
                }
            }

            return result.ToString();
        }

        private string PrepareAuthHeader(string authHeader)
        {
            var encodedParams =
                string.Join(
                    ",",
                    (from param in authHeader.Split('&')
                     let args = param.Split('=')
                     where !args[0].Contains("realm")
                     select args[0] + "=\"" + args[1] + "\"")
                    .ToArray());

            return "OAuth " + encodedParams;
        }

        /// <summary>
        /// Submit a web request using oAuth.
        /// </summary>
        /// <param name="method">GET or POST</param>
        /// <param name="url">The full url, including the querystring.</param>
        /// <param name="postData">Data to post (querystring format)</param>
        /// <returns>The web server response.</returns>
        public string oAuthWebRequest(HttpMethod method, string url, string postData, string callback)
        {
            string outUrl = "";
            string querystring = "";
            string ret = "";

            //Setup postData for signing.
            //Add the postData to the querystring.
            if (method == HttpMethod.POST)
            {
                if (postData.Length > 0)
                {
                    var qs =
                        (from nameValPair in postData.Split('&')
                         let pair = nameValPair.Split('=')
                         select new
                         {
                             Key = pair[0],
                             Value = pair[1]
                         })
                        .ToDictionary(
                            pair => pair.Key,
                            pair => pair.Value);

                    postData = "";
                    foreach (string key in qs.Keys)
                    {
                        if (postData.Length > 0)
                        {
                            postData += "&";
                        }
                        qs[key] = HttpUtility.UrlDecode(qs[key]);
                        qs[key] = this.UrlEncode(qs[key]);
                        postData += key + "=" + qs[key];

                    }
                    if (url.IndexOf("?") > 0)
                    {
                        url += "&";
                    }
                    else
                    {
                        url += "?";
                    }
                    url += postData;
                }
            }

            Uri uri = new Uri(url);

            string nonce = this.GenerateNonce();
            string timeStamp = this.GenerateTimeStamp();

            //Generate Signature
            string sig = this.GenerateSignature(uri,
                this.OAuthConsumerKey,
                this.OAuthConsumerSecret,
                this.OAuthToken,
                this.OAuthTokenSecret,
                this.OAuthVerifier,
                TwitterParameterUrlEncode(callback),
                method.ToString(),
                timeStamp,
                nonce,
                out outUrl,
                out querystring);

            querystring += "&oauth_signature=" + HttpUtility.UrlEncode(sig);

            //Convert the querystring to postData
            if (method == HttpMethod.POST)
            {
                postData = querystring;
                querystring = "";
            }

            ret = WebRequest(method, outUrl, querystring, postData);

            return ret;
        }

        /// <summary>
        /// Web Request Wrapper
        /// </summary>
        /// <param name="method">Http Method</param>
        /// <param name="url">Full url to the web resource</param>
        /// <param name="postData">Data to post in querystring format</param>
        /// <returns>The web server response.</returns>
        public string WebRequest(HttpMethod method, string url, string authHeader, string postData)
        {
            HttpWebRequest webRequest = null;
            string responseData = "";

            webRequest = System.Net.WebRequest.Create(url) as HttpWebRequest;
            webRequest.Method = method.ToString();
#if !SILVERLIGHT
            webRequest.ServicePoint.Expect100Continue = false;
            webRequest.UserAgent = OAuthUserAgent; 
#endif
            webRequest.Headers[HttpRequestHeader.Authorization] = PrepareAuthHeader(authHeader);

            if (method == HttpMethod.POST)
            {
                webRequest.ContentType = "application/x-www-form-urlencoded";

                byte[] postDataBytes = Encoding.UTF8.GetBytes(postData);

#if SILVERLIGHT
                // TODO: work in progress
                webRequest.BeginGetRequestStream(
                    new AsyncCallback(
                        ar =>
                        {
                            using (var requestStream = webRequest.EndGetRequestStream(ar))
                            {
                                requestStream.Write(postDataBytes, 0, postDataBytes.Length);
                            }
                        }), null);
#else
                var resetEvent = new ManualResetEvent(initialState: false);

                webRequest.BeginGetRequestStream(
                    new AsyncCallback(
                        ar =>
                        {
                            using (var requestStream = webRequest.EndGetRequestStream(ar))
                            {
                                requestStream.Write(postDataBytes, 0, postDataBytes.Length);
                            }
                            resetEvent.Set();
                        }), null);

                resetEvent.WaitOne();
#endif
            }

#if !SILVERLIGHT
            responseData = WebResponseGet(webRequest); 
#endif

            webRequest = null;

            return responseData;
        }

        /// <summary>
        /// Process the web response.
        /// </summary>
        /// <param name="webRequest">The request object.</param>
        /// <returns>The response data.</returns>
        public string WebResponseGet(HttpWebRequest webRequest)
        {
            string responseData = "";

            var resetEvent = new ManualResetEvent(initialState: false);
            HttpWebResponse res = null;

            webRequest.BeginGetResponse(
                new AsyncCallback(
                    ar =>
                    {
                        res = webRequest.EndGetResponse(ar) as HttpWebResponse;
                        using (var respStream = res.GetResponseStream())
                        using (var respReader = new StreamReader(respStream))
                        {
                            responseData = respReader.ReadToEnd();
                        }
                        resetEvent.Set();
                    }), null);

            resetEvent.WaitOne();

            return responseData;
        }

        #region - Async -

        public HttpWebRequest GetHttpRequest(Uri oauthUrl, Uri callbackUrl)
        {
            string signedUrl = null;
            string queryString = null;
            string callback = callbackUrl == null ? "oob" : callbackUrl.ToString();
            GetOAuthQueryString(HttpMethod.GET, oauthUrl.ToString(), callback, out signedUrl, out queryString);
            var req = System.Net.WebRequest.Create(signedUrl + "?" + queryString) as HttpWebRequest;
            req.Method = HttpMethod.GET.ToString();
            //req.Headers[HttpRequestHeader.Authorization] =
            //    GetOAuthHeader(oauthUrl, callbackUrl);
#if !SILVERLIGHT
            req.ServicePoint.Expect100Continue = false;
            req.UserAgent = OAuthUserAgent;
#endif
            return req;
        }

        /// <summary>
        /// Gets a signed OAuth Header
        /// </summary>
        /// <param name="url">Request Url</param>
        /// <param name="callbackUrl">Callback Url</param>
        /// <returns></returns>
        public string GetOAuthHeader(Uri url, Uri callbackUrl)
        {
            string outUrl = string.Empty;
            string queryString = string.Empty;
            string nonce = this.GenerateNonce();
            string timeStamp = this.GenerateTimeStamp();
            string callback = callbackUrl == null ? "oob" : callbackUrl.ToString();

            //Generate Signature
            string sig = this.GenerateSignature(url,
                this.OAuthConsumerKey,
                this.OAuthConsumerSecret,
                this.OAuthToken,
                this.OAuthTokenSecret,
                this.OAuthVerifier,
                TwitterParameterUrlEncode(callback),
                HttpMethod.GET.ToString(),
                timeStamp,
                nonce,
                out outUrl,
                out queryString);

            queryString += "&oauth_signature=" + HttpUtility.UrlEncode(sig);

            return PrepareAuthHeader(queryString);
        }

        /// <summary>
        /// Asynchronous request for OAuth request token
        /// </summary>
        /// <param name="oauthRequestTokenUrl">Url to make initial request on</param>
        /// <param name="oauthAuthorizeUrl">Url to send user to for authorization</param>
        /// <param name="twitterCallbackUrl">Url for Twitter to redirect to after authorization (null for Pin authorization)</param>
        /// <param name="readOnly">Should access be read-only</param>
        /// <param name="forceLogin">Should user be forced to log in to authorize this app</param>
        /// <param name="authorizationCallback">Lambda to let program perform redirect to authorization page</param>
        /// <param name="authenticationCompleteCallback">Lambda to invoke to let user know when authorization completes</param>
        public void GetRequestTokenAsync(
            Uri oauthRequestTokenUrl, 
            Uri oauthAuthorizeUrl, 
            Uri twitterCallbackUrl, 
            bool readOnly, bool forceLogin, 
            Action<string> authorizationCallback, 
            Action<TwitterAsyncResponse<object>> authenticationCompleteCallback)
        {
            HttpWebRequest req = GetHttpRequest(oauthRequestTokenUrl, twitterCallbackUrl);

            req.BeginGetResponse(
                new AsyncCallback(
                    ar =>
                    {
                        var twitterResponse = new TwitterAsyncResponse<object>();

                        try
                        {
                            string requestTokenResponse = string.Empty;

                            var res = req.EndGetResponse(ar) as HttpWebResponse;

                            using (var respStream = res.GetResponseStream())
                            using (var respReader = new StreamReader(respStream))
                            {
                                requestTokenResponse = respReader.ReadToEnd();
                            }

                            string authorizationUrl = PrepareAuthorizeUrl(oauthAuthorizeUrl.ToString(), readOnly, forceLogin, requestTokenResponse);

                            authorizationCallback(authorizationUrl);
                        }
                        catch (TwitterQueryException tqe)
                        {
                            twitterResponse.Status = TwitterErrorStatus.TwitterApiError;
                            twitterResponse.Message = "Error while communicating with Twitter. Please see Error property for details.";
                            twitterResponse.Error = tqe;
                        }
                        catch (Exception ex)
                        {
                            twitterResponse.Status = TwitterErrorStatus.TwitterApiError;
                            twitterResponse.Message = "Error during LINQ to Twitter processing. Please see Error property for details.";
                            twitterResponse.Error = ex;
                        }
                        finally
                        {
                            if (authenticationCompleteCallback != null)
                            {
                                authenticationCompleteCallback(twitterResponse); 
                            }
                        }
                    }), null);
        }

        /// <summary>
        /// Asynchronous request for OAuth access token
        /// </summary>
        /// <param name="verifier">Verification token provided by Twitter after user authorizes (7-digit number for Pin authorization too)</param>
        /// <param name="oauthAccessTokenUrl">Access token URL</param>
        /// <param name="twitterCallbackUrl">URL for your app that Twitter redirects to after authorization (null for Pin authorization)</param>
        /// <param name="authenticationCompleteCallback">Callback to application after response completes (contains UserID and ScreenName)</param>
        public void GetAccessTokenAsync(
            string verifier,
            Uri oauthAccessTokenUrl,
            Uri twitterCallbackUrl,
            Action<TwitterAsyncResponse<UserIdentifier>> authenticationCompleteCallback)
        {
            this.OAuthVerifier = verifier;

            HttpWebRequest req = GetHttpRequest(oauthAccessTokenUrl, twitterCallbackUrl);

            req.BeginGetResponse(
                new AsyncCallback(
                    ar =>
                    {
                        string screenName = string.Empty;
                        string userID = string.Empty;

                        var twitterResponse = new TwitterAsyncResponse<UserIdentifier>();

                        try
                        {
                            string accessTokenResponse = string.Empty;

                            var res = req.EndGetResponse(ar) as HttpWebResponse;

                            using (var respStream = res.GetResponseStream())
                            using (var respReader = new StreamReader(respStream))
                            {
                                accessTokenResponse = respReader.ReadToEnd();
                            }

                            ProcessAccessTokenResponse(ref screenName, ref userID, accessTokenResponse);
                        }
                        catch (TwitterQueryException tqe)
                        {
                            twitterResponse.Status = TwitterErrorStatus.TwitterApiError;
                            twitterResponse.Message = "Error while communicating with Twitter. Please see Error property for details.";
                            twitterResponse.Error = tqe;
                        }
                        catch (Exception ex)
                        {
                            twitterResponse.Status = TwitterErrorStatus.TwitterApiError;
                            twitterResponse.Message = "Error during LINQ to Twitter processing. Please see Error property for details.";
                            twitterResponse.Error = ex;
                        }
                        finally
                        {
                            if (authenticationCompleteCallback != null)
                            {
                                twitterResponse.State =
                                    new UserIdentifier
                                    {
                                        ID = userID,
                                        UserID = userID,
                                        ScreenName = screenName
                                    };
                                authenticationCompleteCallback(twitterResponse); 
                            }
                        }
                    }), null);

        }

        #endregion
    }
}