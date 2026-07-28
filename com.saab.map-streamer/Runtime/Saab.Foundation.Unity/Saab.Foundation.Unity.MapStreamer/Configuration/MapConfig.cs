using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer
{
    public enum MapSource
    {
        Remote,
        LocalDownloaded,
    }

    [CreateAssetMenu(
        fileName = "MapConfig",
        menuName = "Saab/Map Streamer/Map Config")]
    public sealed class MapConfig : ScriptableObject
    {
        [SerializeField]
        private MapSource source = MapSource.Remote;

        [SerializeField]
        private string remoteMapUrl =
            "http://gizmosdk.blob.core.windows.net/maps/stock/map.gzd";

        [SerializeField]
        private string localDownloadedMapUrl =
            "./OfflineMaps/stock/map.gzd";

        public MapSource Source => source;

        public string MapUrl =>
            source == MapSource.LocalDownloaded
                ? localDownloadedMapUrl
                : remoteMapUrl;
    }
}
