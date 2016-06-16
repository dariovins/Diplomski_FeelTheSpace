using Windows.Devices.Enumeration;

namespace FeelTheSpace
{
    internal class PairedDeviceInfo
    {
        internal PairedDeviceInfo(DeviceInformation deviceInfo)
        {
            this.DeviceInfo = deviceInfo;
            this.ID = this.DeviceInfo.Id;
            this.Name = this.DeviceInfo.Name;
        }

        public string Name { get; private set; }
        public string ID { get; private set; }
        public DeviceInformation DeviceInfo { get; private set; }
    }
}