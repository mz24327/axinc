/* AILIA Unity Plugin Face Detector Sample */
/* Copyright 2022 AXELL CORPORATION */

using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.UI;

namespace ailiaSDK {
	public class AiliaFaceDetectorsSample : AiliaRenderer {
        public enum FaceDetectorModels
        {
            blazeface,
            facemesh
        }

		[SerializeField]
		private FaceDetectorModels ailiaModelType = FaceDetectorModels.blazeface;
		[SerializeField]
		private GameObject UICanvas = null;

		//Settings
		[SerializeField]
		private bool gpu_mode = false;
		[SerializeField]
		private int camera_id = 0;
		[SerializeField]
		private bool debug = false;

		//Result
		RawImage raw_image = null;
		Text label_text = null;
		Text mode_text = null;

		//Preview
		private Texture2D preview_texture = null;

		//AILIA
		private AiliaModel ailia_face_detector = new AiliaModel();
		private AiliaModel ailia_face_recognizer = new AiliaModel();

		private AiliaBlazeface blaze_face = new AiliaBlazeface();
		private AiliaFaceMesh face_mesh = new AiliaFaceMesh();

		private AiliaCamera ailia_camera = new AiliaCamera();
		private AiliaDownload ailia_download = new AiliaDownload();

		// AILIA open file
		private bool FileOpened = false;

		private void CreateAiliaDetector(FaceDetectorModels modelType)
		{
			string asset_path = Application.temporaryCachePath;
			Debug.Log(asset_path);
			var urlList = new List<ModelDownloadURL>();
			if (gpu_mode)
			{
				ailia_face_detector.Environment(Ailia.AILIA_ENVIRONMENT_TYPE_GPU);
				ailia_face_recognizer.Environment(Ailia.AILIA_ENVIRONMENT_TYPE_GPU);
			}
			switch (modelType)
			{		
				case FaceDetectorModels.blazeface:
					mode_text.text = "ailia face Detector";

					urlList.Add(new ModelDownloadURL() { folder_path = "blazeface", file_name = "blazeface.onnx.prototxt" });
					urlList.Add(new ModelDownloadURL() { folder_path = "blazeface", file_name = "blazeface.onnx" });

					StartCoroutine(ailia_download.DownloadWithProgressFromURL(urlList, () =>
					{
						FileOpened = ailia_face_detector.OpenFile(asset_path + "/blazeface.onnx.prototxt", asset_path + "/blazeface.onnx");
					}));

					break;

				case FaceDetectorModels.facemesh:
					mode_text.text = "ailia face Recognizer";

					urlList.Add(new ModelDownloadURL() { folder_path = "blazeface", file_name = "blazeface.onnx.prototxt" });
					urlList.Add(new ModelDownloadURL() { folder_path = "blazeface", file_name = "blazeface.onnx" });
					urlList.Add(new ModelDownloadURL() { folder_path = "facemesh", file_name = "facemesh.onnx.prototxt" });
					urlList.Add(new ModelDownloadURL() { folder_path = "facemesh", file_name = "facemesh.onnx" });

					StartCoroutine(ailia_download.DownloadWithProgressFromURL(urlList, () =>
					{
						FileOpened = ailia_face_detector.OpenFile(asset_path + "/blazeface.onnx.prototxt", asset_path + "/blazeface.onnx");
						FileOpened = ailia_face_recognizer.OpenFile(asset_path + "/facemesh.onnx.prototxt", asset_path + "/facemesh.onnx");
					}));

					break;

				default:
					Debug.Log("Others ailia models are working in progress.");
					break;
			}
		}


		private void DestroyAiliaDetector()
		{
			ailia_face_detector.Close();
			ailia_face_recognizer.Close();
		}

		// Use this for initialization
		void Start()
		{
			SetUIProperties();
			CreateAiliaDetector(ailiaModelType);
			ailia_camera.CreateCamera(camera_id);
		}

		// Update is called once per frame
		void Update()
		{
			if (!ailia_camera.IsEnable())
			{
				return;
			}
			if (!FileOpened)
			{
				return;
			}

			//Clear result
			Clear();

			//Get camera image
			int tex_width = ailia_camera.GetWidth();
			int tex_height = ailia_camera.GetHeight();
			if (preview_texture == null)
			{
				preview_texture = new Texture2D(tex_width, tex_height);
				raw_image.texture = preview_texture;
			}
			Color32[] camera = ailia_camera.GetPixels32();

			//BlazeFace
			long start_time = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
			List<AiliaBlazeface.FaceInfo> result_detections = blaze_face.Detection(ailia_face_detector, camera, tex_width, tex_height);
			long end_time = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
			long detection_time = (end_time - start_time);

			//Draw result
			if(ailiaModelType==FaceDetectorModels.blazeface){
				for (int i = 0; i < result_detections.Count; i++)
				{
					AiliaBlazeface.FaceInfo face = result_detections[i];
					int fw = (int)(face.width * tex_width);
					int fh = (int)(face.height * tex_height);
					int fx = (int)(face.center.x * tex_width) - fw / 2;
					int fy = (int)(face.center.y * tex_height) - fh / 2;
					DrawRect2D(Color.blue, fx, fy, fw, fh, tex_width, tex_height);

					for (int k = 0; k < AiliaBlazeface.NUM_KEYPOINTS; k++)
					{
						int x = (int)(face.keypoints[k].x * tex_width);
						int y = (int)(face.keypoints[k].y * tex_height);
						DrawRect2D(Color.blue, x, y, 1, 1, tex_width, tex_height);
					}
				}
			}

			//Compute facemesh
			long recognition_time = 0;
			if(ailiaModelType==FaceDetectorModels.facemesh){
				//Compute
				long rec_start_time = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
				List<AiliaFaceMesh.FaceMeshInfo> result_facemesh = face_mesh.Detection(ailia_face_recognizer, camera, tex_width, tex_height, result_detections, debug);
				long rec_end_time = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
				recognition_time = (rec_end_time - rec_start_time);

				//Draw result
				for (int i = 0; i < result_facemesh.Count; i++)
				{
					AiliaFaceMesh.FaceMeshInfo face = result_facemesh[i];

					int fw = (int)(face.width * tex_width);
					int fh = (int)(face.height * tex_height);
					int fx = (int)(face.center.x * tex_width) - fw / 2;
					int fy = (int)(face.center.y * tex_height) - fh / 2;
					DrawAffine2D(Color.green, fx, fy, fw, fh, tex_width, tex_height, face.theta);

					float scale = 1.0f * fw / AiliaFaceMesh.DETECTION_WIDTH;

					float ss=(float)System.Math.Sin(face.theta);
					float cs=(float)System.Math.Cos(face.theta);

					for (int k = 0; k < AiliaFaceMesh.NUM_KEYPOINTS; k++)
					{
						int x = (int)(face.center.x * tex_width  + ((face.keypoints[k].x - AiliaFaceMesh.DETECTION_WIDTH/2) * cs + (face.keypoints[k].y - AiliaFaceMesh.DETECTION_HEIGHT/2) * -ss)* scale);
						int y = (int)(face.center.y * tex_height + ((face.keypoints[k].x - AiliaFaceMesh.DETECTION_WIDTH/2) * ss + (face.keypoints[k].y - AiliaFaceMesh.DETECTION_HEIGHT/2) *  cs)* scale);
						DrawRect2D(Color.green, x, y, 1, 1, tex_width, tex_height);
					}
				}
			}

			if (label_text != null)
			{
				if(ailiaModelType==FaceDetectorModels.facemesh){
					label_text.text = detection_time + "ms + " + recognition_time + "ms\n" + ailia_face_detector.EnvironmentName();
				}else{
					label_text.text = detection_time + "ms\n" + ailia_face_detector.EnvironmentName();
				}
			}

			//Apply
			preview_texture.SetPixels32(camera);
			preview_texture.Apply();
		}

		void SetUIProperties()
		{
			if (UICanvas == null) return;
			// Set up UI for AiliaDownloader
			var downloaderProgressPanel = UICanvas.transform.Find("DownloaderProgressPanel");
			ailia_download.DownloaderProgressPanel = downloaderProgressPanel.gameObject;
			// Set up lines
			line_panel = UICanvas.transform.Find("LinePanel").gameObject;
			lines = UICanvas.transform.Find("LinePanel/Lines").gameObject;
			line = UICanvas.transform.Find("LinePanel/Lines/Line").gameObject;
			text_panel = UICanvas.transform.Find("TextPanel").gameObject;
			text_base = UICanvas.transform.Find("TextPanel/TextHolder").gameObject;

			raw_image = UICanvas.transform.Find("RawImage").gameObject.GetComponent<RawImage>();
			label_text = UICanvas.transform.Find("LabelText").gameObject.GetComponent<Text>();
			mode_text = UICanvas.transform.Find("ModeLabel").gameObject.GetComponent<Text>();
		}

		void OnApplicationQuit()
		{
			DestroyAiliaDetector();
			ailia_camera.DestroyCamera();
		}

		void OnDestroy()
		{
			DestroyAiliaDetector();
			ailia_camera.DestroyCamera();
		}
	}
}

