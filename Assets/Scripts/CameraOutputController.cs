﻿using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

[RequireComponent(typeof(Camera))]
public class CameraOutputController : MonoBehaviour
{
    private class GPUReader : IDisposable
    {
        private NativeArray<uint> outputArray;
        private AsyncGPUReadbackRequest request;
        private bool hasRequest = false;
        private bool outputReady = false;

        public GPUReader(int size)
        {
            outputArray = new NativeArray<uint>(size, Allocator.Persistent);
        }

        public void Read(RenderTexture renderTexture)
        {
            request = AsyncGPUReadback.RequestIntoNativeArray(ref outputArray, renderTexture, 0, TextureFormat.ARGB32, OnCompleteReadback);
            hasRequest = true;
        }


        public bool WriteTo(Texture2D texture)
        {
            if (hasRequest)
            {
                request.WaitForCompletion();
            }
            if (outputReady)
            {
                outputReady = false;
                texture.LoadRawTextureData(outputArray);
                texture.Apply();
                return true;
            }
            return false;
        }

        void OnCompleteReadback(AsyncGPUReadbackRequest request)
        {
            hasRequest = false;
            if (request.hasError)
            {
                Debug.Log("GPU readback error detected.");
                return;
            }
            outputReady = true;
        }

        public void Dispose()
        {
            if (hasRequest)
            {
                request.WaitForCompletion();
            }
            outputArray.Dispose();
        }
    }

    private const int READERS_LENGTH = 2;
    private const int CURRENT = 0;
    private const int NEXT = READERS_LENGTH - 1;

    private Camera mCamera;
    public RenderTexture renderTexture;
    private GPUReader[] readers = new GPUReader[READERS_LENGTH];
    private Texture2D virtualPhoto;
    private float lastSaved = 0;
    private const int width = 128;
    private const int height = 80;


    private volatile CarSocket socket;

    private void Start()
    {
        mCamera = GetComponent<Camera>();
        renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        renderTexture.antiAliasing = 2;
        for (int i = 0; i < readers.Length; ++i)
        {
            readers[i] = new GPUReader(width * height);
        }
        virtualPhoto = new Texture2D(width, height, TextureFormat.ARGB32, false);
        Debug.Log("CameraOutputController started");
        mCamera.rect = new Rect(0, 0, 1, 1);
        mCamera.aspect = 1.0f * width / height;
        mCamera.targetTexture = renderTexture;
        mCamera.enabled = true;
        RenderPipelineManager.endFrameRendering += OnEndFrameRendering;
    }

    void OnEndFrameRendering(ScriptableRenderContext context, Camera[] cameras)
    {
        if (socket == null) return;
        if (Time.time < lastSaved + 0.03 || socket.SendQueueSize() > 1)
        {
            return;
        }
        lastSaved = Time.time;

        if (Application.platform == RuntimePlatform.LinuxPlayer) {
            SendSync();
        } else {
            SendAsync();
        } 
    }

    void SendAsync() 
    {
        readers[NEXT].Read(renderTexture);

        if (readers[CURRENT].WriteTo(virtualPhoto))
        {
            socket.Send(encodeFrame(virtualPhoto));
        }

        Roll(readers);
    }

    void SendSync()
    {
        mCamera.rect = new Rect(0, 0, 1, 1);
        mCamera.aspect = 1.0f * width / height;
        // recall that the height is now the "actual" size from now on

        //RenderTexture tempRT = RenderTexture.GetTemporary(width, height, 24);
        // the 24 can be 0,16,24, formats like
        // RenderTextureFormat.Default, ARGB32 etc.
        //tempRT.antiAliasing = 2;

        mCamera.targetTexture = renderTexture;
        mCamera.Render();

        RenderTexture.active = renderTexture;
        virtualPhoto.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        virtualPhoto.Apply();

        RenderTexture.active = null; //can help avoid errors 
        mCamera.targetTexture = null;
        //RenderTexture.ReleaseTemporary(tempRT);

        socket.Send(encodeFrame(virtualPhoto));
    }

    private void OnDestroy()
    {
        if (this.socket != null)
        {
            this.socket = null;
        }
        for (int i = 0; i < readers.Length; ++i)
        {
            if (readers[i] != null)
            {
                readers[i].Dispose();
            }
            readers[i] = null;
        }
    }

    private byte[] encodeFrame(Texture2D virtualPhoto)
    {
        byte[] data = virtualPhoto.EncodeToPNG();

        if (data.Length > 65535) throw new Exception("Max image size exceeded");
        byte lowerByte = (byte)(data.Length & 0xff);
        byte higherByte = (byte)((data.Length & 0xff00) >> 8);
        //Debug.Log("Length " + data.Length + " " + higherByte + " " + lowerByte);
        byte[] lengthAsBytes = new byte[] { higherByte, lowerByte };
        byte[] encodedBytes = lengthAsBytes.Concat(data).ToArray();
        return encodedBytes;
    }

    public void SetSocket(CarSocket socket)
    {
        if (this.socket != null) return;
        this.socket = socket;
    }

    private static void Roll(GPUReader[] array)
    {
        GPUReader tmp = array[0];
        for (int i = 1; i < array.Length; ++i)
        {
            array[i - 1] = array[i];
        }
        array[array.Length - 1] = tmp;
    }
}