using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace GraphQlClientUnity
{
    public static class GraphQlClientUtils
	{
        public static IEnumerator Send(string url, Dictionary<string, string> headers, string json, Action<string> callback)
        {
            return Send(url, headers, json, (_json, error) => callback(_json));
        }

        public static IEnumerator Send(string url, Dictionary<string, string> headers, string json, Action<string, string> callback)
        {
            byte[] sentData = System.Text.Encoding.UTF8.GetBytes(json);
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(sentData);
            request.downloadHandler = new DownloadHandlerBuffer();

            foreach (var h in headers)
            {
                request.SetRequestHeader(h.Key, h.Value);
            }

            yield return request.SendWebRequest();

            while (!request.isDone)
            {
                yield return 0;
            }

            if (request.isNetworkError)
            {
                callback(null, request.error);
            }

            callback(request.downloadHandler.text, null);
        }
    }
}
