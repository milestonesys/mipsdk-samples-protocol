using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace SecurityPermission
{
    public class SecurityNamespaceModel
    {
        public string name { get; set; }
        public string displayName { get; set; }
        public string id { get; set; }
        public string resourceType { get; set; }
        public bool userDefined { get; set; }
        public SecurityAction[] securityActions { get; set; }

        internal ulong LookupPermissionMask(string actionName)
        {
            SecurityAction securityAction = securityActions.FirstOrDefault(a => a.name == actionName);
            if (securityAction == null)
                return 0;
            return securityAction.securityMask;
        }
    }

    public class SecurityAction
    {
        public string name { get; set; }
        public string displayName { get; set; }
        public bool visible { get; set; }
        public ulong securityMask { get; set; }
    }

    public class EffectivePermissionsModel
    {
        public string id { get; set; }
        public bool isAdministrator { get; set; }

        public ObjectPermissionsModel[] objectPermissions { get; set; }

        internal ulong LookupMask(Guid objectId)
        {
            if (isAdministrator) return (ulong)0xffffffffffffffff;

            ObjectPermissionsModel o = objectPermissions.FirstOrDefault(op => op.id == objectId);
            if (o == null) return 0;
            return o.mask;
        }
    }

    public class ObjectPermissionsModel
    {
        public Guid id { set; get; }
        public ulong mask { set; get; }
    }

    public class CameraSimpleModel
    {
        public Guid id { set; get; }
        public string displayName { set; get; }
        // We dont need the rest in this sample
    }
}
