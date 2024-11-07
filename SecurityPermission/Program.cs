using System;
using System.Diagnostics;
using System.Net;
using System.Security;

namespace SecurityPermission
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //TODO: fill in these fields with correct values
            string username = "basic"; 
            string password = "password";     

            string adUsername = "adUser";
            string adPassword = "adPassword";
            string adDomain = "adDomain";

            //TODO: Select to login as basic or windows user
            bool adUser = false;

            //TODO: fill in correct XProtect API Gateway address (Management Server address)
            bool secure = false;
            string serverAddress = "localhost";
            int port = 80;

            Uri serverUri = new Uri(string.Format("{0}://{1}:{2}/", secure ? "https" : "http", serverAddress, port));

            var uriHelper = new UriHelper(serverUri);
            bool serverOnline = uriHelper.Initialize();
            if (!serverOnline)
            {
                Console.WriteLine("Unable to login on: " + serverAddress);
                Console.ReadKey();
                return;
            }

            var client = new AuthenticatedClient(uriHelper);
            try
            {
                if (adUser)
                {
                    if (string.IsNullOrEmpty(adUsername))
                        client.TryLoginWindows(CredentialCache.DefaultCredentials);
                    else
                        client.TryLoginWindows(new NetworkCredential(adUsername, adPassword, adDomain));
                }
                else
                {
                    client.TryLoginBasic(username, password);
                }

                //Get and dump id for all defined securityNamespaces
                var allNamespaces = client.HttpGetArray<SecurityNamespaceModel>(uriHelper.ApiGatewayRestUri.ToString() + "/securityNamespaces");

                Console.WriteLine("Show all Security Namespaces");
                foreach (var ns in allNamespaces)
                {
                    Console.WriteLine("    {0} - has Id: {1}", ns.displayName, ns.id);
                }

                // Get and dump namespace information
                var cameraSecurityNamespace = client.HttpGetData<SecurityNamespaceModel>(uriHelper.ApiGatewayRestUri.ToString() + "/securityNamespaces/623D03F8-C5D5-46BC-A2F4-4C03562D4F85");

                Console.WriteLine("Camera Security Namespace has these possible security actions: ");
                foreach (var action in cameraSecurityNamespace.securityActions) 
                {
                    Console.WriteLine("    {0} - bit position: {1:X}", action.displayName, action.securityMask);
                }

                // Get just one camera
                Console.WriteLine("Get first camera an dump its permissions for above username:");
                var cameras = client.HttpGetArray<CameraSimpleModel>(uriHelper.ApiGatewayRestUri.ToString() + "/cameras");
                if (cameras.Length > 0)
                {
                    var cameraFirst = cameras[0];

                    //Get effective permissions for all cameras I can access
                    var effectivePermissionsModel = client.HttpGetData<EffectivePermissionsModel>(uriHelper.ApiGatewayRestUri.ToString() + "/effectivePermissions/" + cameraSecurityNamespace.id);

                    if (effectivePermissionsModel.isAdministrator)
                    {
                        Console.WriteLine("    You are part of the administrator role and have access to all objects");
                    }
                    else
                    { 
                        // Loopup my permission mask for the found camera
                        ulong mask = effectivePermissionsModel.LookupMask(cameraFirst.id);
                        Console.WriteLine("    Effective bit mask: {0:X}", mask);
                        foreach (var action in cameraSecurityNamespace.securityActions)
                        {
                            Console.WriteLine("       {0} - bit position: {1:X} --- has access:{2}", 
                                action.displayName, 
                                action.securityMask, 
                                (action.securityMask & mask)!= 0 ? "true" : "false");
                        }
                    }

                    // The intended use could look like this:
                    ulong definedMask = cameraSecurityNamespace.LookupPermissionMask("EXPORT");
                    if ((effectivePermissionsModel.LookupMask(cameraFirst.id) & definedMask) != 0)
                    {
                        // Export is permitted
                    }

                    //-------------------------------- another sample -----------------
                    // Say we just need to check one camera, and are not expected to work with other cameras soon

                    // Get camera ns definition
                    var ns2 = client.HttpGetData<SecurityNamespaceModel>(uriHelper.ApiGatewayRestUri.ToString() + "/securityNamespaces/623D03F8-C5D5-46BC-A2F4-4C03562D4F85");
                    // Get effective permission for just one camera (Or comma seperated list of camera ids)
                    var effectiveP2 = client.HttpGetData<EffectivePermissionsModel>(uriHelper.ApiGatewayRestUri.ToString() + "/effectivePermissions/" + cameraSecurityNamespace.id +"?objectIds="+cameraFirst.id);

                    ulong definedMaskExport = cameraSecurityNamespace.LookupPermissionMask("EXPORT");   // Is EXPORT allowed
                    if ((effectiveP2.LookupMask(cameraFirst.id) & definedMaskExport) != 0)
                        Console.WriteLine("  Sample 2 - EXPORT is permitted");
                    else
                        Console.WriteLine("  Sample 2 - EXPORT is NOT permitted");

                    ulong definedMaskPTZ = cameraSecurityNamespace.LookupPermissionMask("PTZ_CONTROL"); // Is PTZ control allowed
                    if ((effectiveP2.LookupMask(cameraFirst.id) & definedMaskPTZ) != 0)
                        Console.WriteLine("  Sample 2 - EXPORT is permitted");
                    else
                        Console.WriteLine("  Sample 2 - EXPORT is NOT permitted");

                }
            }
            catch (SecurityException ex)
            {
                Console.WriteLine("Logon error:" + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            Console.WriteLine("Press any key to stop");
            Console.ReadKey();
        }


    }
}
