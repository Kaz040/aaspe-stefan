/*
Copyright (c) 2018-2023 Festo SE & Co. KG <https://www.festo.com/net/de_de/Forms/web/contact_international>
Author: Michael Hoffmeister

This source code is licensed under the Apache License 2.0 (see LICENSE.txt).

This source code may use other Open Source software components (see LICENSE.txt).
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AasxPredefinedConcepts;
using Aas = AasCore.Aas3_0;
using AdminShellNS;
using AdminShellNS.DiaryData;
using Extensions;
using AasxIntegrationBase;
using AasxPredefinedConcepts.AssetInterfacesDescription;
using FluentModbus;
using System.Net;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Net.Http;
using AasxIntegrationBase.AdminShellEvents;
using System.Drawing;
using System.Security.Policy;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO;

namespace AasxPluginAssetInterfaceDescription
{
    public class AidHttpConnection : AidBaseConnection
    {
        public HttpClient Client;

        override public bool Open()
        {
            // pretty simple
            Client = new HttpClient();

            if (TimeOutMs >= 10)
                Client.Timeout = new TimeSpan(0, 0, 0, 0, milliseconds: (int)TimeOutMs);

            return true;
        }

        override public bool IsConnected()
        {
            // nothing to do, this simple http connection is stateless
            return Client != null;
        }

        override public void Close()
        {
            // nothing to do, this simple http connection is stateless
        }        

        override public async Task<int> UpdateItemValueAsync(AidIfxItemStatus item)
        {
            // access
            if (item?.FormData?.Href?.HasContent() != true
                || item.FormData.Htv_methodName?.HasContent() != true
                || !IsConnected())
                return 0;
            int res = 0;

            // GET?
            if (item.FormData.Htv_methodName.Trim().ToLower() == "get")
            {
                try
                {
                    // get combined uri
                    var url = new Uri(TargetUri, item.FormData.Href);

                    // get response (synchronously)
                    var response = await Client.GetAsync(url);

                    // ok?
                    if (response.IsSuccessStatusCode)
                    {
                        // set internal value
                        var strval = await response.Content.ReadAsStringAsync();
                        item.Value = strval;
                        res = 1;

                        // notify
                        NotifyOutputItems(item, strval);
                    }
                } catch (Exception ex)
                {
                    ;
                }
            }

            return res;
        }

        override public async Task<bool> ExecuteBindingAsync(AssetBindingService item)
        {
            bool IsValid = false;
            if (item?.FormData?.Href?.HasContent() != true
                || item.FormData.Htv_methodName?.HasContent() != true
                || !IsConnected())
                return false;

            try
            {
                var urlPath = item?.FormData.Href;
                var urlPathandParameter = urlPath.Split('?');
                var paremeter = urlPathandParameter[1];
                if(paremeter.Contains("challenge"))
                {
                    //Get the challenge value in xxxxxxxxxxxxxx
                    var challenge = paremeter.Split('=')[1];

                    //Generate random bytes according to challenge length
                    Random random = new Random();
                    byte[] randomBytes = new byte[challenge.Length];
                    random.NextBytes(randomBytes);

                    //Encode the byte generated to base64
                    challenge = Convert.ToBase64String(randomBytes);

                    //recreate the URL path
                    urlPath = $"{urlPathandParameter[0]}?challenge={challenge}";

                    //create a URI object with the new urlpath
                    
                    var url = new Uri(TargetUri, urlPath); //http://127.0.0.1:5000/authenticate/authenticate.cgi?challenge={challenge}

                    //The response will be a JSON payload containing three fields. Nonce, AssetID and certificate
                    var response = await Client.GetAsync(url);
                    if(response.IsSuccessStatusCode && response.Content.Headers.ContentType.MediaType == "application/json")
                    {
                        var responseValue = await response.Content.ReadAsStringAsync();
                    
                        //verify
                        IsValid = verifyBinding(item, responseValue, randomBytes);

                    }
                }
                //var url = new Uri(TargetUri, item?.FormData.Href);
                //if (item?.FormData.Htv_headers.Htv_fieldName == "challenge")
                //    strinfieldValue = item?.FormData.Htv_headers.Htv_fieldValue;

                //var response = await Client.GetAsync(url);

                return IsValid;
            }
            catch (Exception ex)
            {
                return IsValid;
            }
            
        }
    }
}
