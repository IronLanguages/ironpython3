using System.Linq;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    [PythonHidden, PythonType("sys.implementation")]
    public class Implementation {
        internal static readonly string _Name = "IronPython";
        internal static readonly string _name = _Name.ToLowerInvariant();
        internal static readonly VersionInfo _version = new VersionInfo();
        internal static readonly int _hexversion = _version.GetHexVersion();

        public readonly string cache_tag = null;
        public readonly string name = _name;
        public readonly VersionInfo version = _version;
        public readonly int hexversion = _hexversion;

        public string __repr__(CodeContext context) {
            var attrs = from attr in PythonOps.GetAttrNames(context, this)
                        where !attr.ToString().StartsWith("_")
                        select string.Format("{0}={1}",
                            attr,
                            PythonOps.Repr(context, PythonOps.GetBoundAttr(context, this, attr.ToString()))
                        );

            return string.Format("{0}({1})",
                PythonOps.GetPythonTypeName(this),
                string.Join(",", attrs.ToArray())
            );
        }
    }

    [PythonType("sys.version_info")]
    public class VersionInfo : PythonTuple {
        public readonly int major;
        public readonly int minor;
        public readonly int micro;
        public readonly string releaselevel;
        public readonly int serial;

        private VersionInfo(int major, int minor, int micro, string releaselevel, int serial)
            : base(new object[] { major, minor, micro, releaselevel, serial }) {
            this.major = major;
            this.minor = minor;
            this.micro = micro;
            this.releaselevel = releaselevel;
            this.serial = serial;
        }

        internal VersionInfo() 
            : this(CurrentVersion.Major, 
                   CurrentVersion.Minor, 
                   CurrentVersion.Micro,
                   CurrentVersion.ReleaseLevel,
                   CurrentVersion.ReleaseSerial) {}

        public override string __repr__(CodeContext context) {
            return string.Format("sys.version_info(major={0}, minor={1}, micro={2}, releaselevel='{3}', serial={4})",
                major, minor, micro, releaselevel, serial);
        }

        internal int GetHexVersion() {
            int hexlevel = 0;
            switch(releaselevel) {
                case "alpha":
                    hexlevel = 0xA;
                    break;

                case "beta":
                    hexlevel = 0xB;
                    break;

                case "candidate":
                    hexlevel = 0xC;
                    break;

                case "final":
                    hexlevel = 0xF;
                    break;
            }

            return (major << 24) |
                   (minor << 16) |
                   (micro << 8) |
                   (hexlevel << 4) |
                   (serial << 0);
        }

        internal string GetVersionString(string _initialVersionString) {
            var version = string.Format("{0}.{1}.{2}{4}{5} ({3})",
                major,
                minor,
                micro,
                _initialVersionString, 
                releaselevel != "final" ? CurrentVersion.ShortReleaseLevel : "",
                releaselevel != "final" ? serial.ToString() : "");
            return version;
        }
    }
}
