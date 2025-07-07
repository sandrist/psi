import cv2
import numpy as np
import mediapipe as mp

class AriaTrackingProcessor:
    def __init__(self):
        # Initialize MediaPipe modules
        self.mp_drawing = mp.solutions.drawing_utils

        self.hands = mp.solutions.hands.Hands(
            static_image_mode=False,
            max_num_hands=2,
            min_detection_confidence=0.5,
            min_tracking_confidence=0.5
        )

        self.pose = mp.solutions.pose.Pose(
            static_image_mode=False,
            model_complexity=1,
            enable_segmentation=False,
            min_detection_confidence=0.5,
            min_tracking_confidence=0.5
        )

        self.face_mesh = mp.solutions.face_mesh.FaceMesh(
            max_num_faces=1,
            refine_landmarks=True,
            min_detection_confidence=0.2,
            min_tracking_confidence=0.2
        )
    def process(self, image_bgr):
        """
        Process the image and overlay tracking results (face, eye, hand, full-body).
        Returns:
            annotated_image, {
                "hands": <np.ndarray>,
                "skeleton": <np.ndarray>,
                "gaze": <np.ndarray>
            }
        """
        annotated_image = image_bgr.copy()
        image_rgb = cv2.cvtColor(annotated_image, cv2.COLOR_BGR2RGB)
        h, w, _ = annotated_image.shape

        hands_data = []
        skeleton_data = []
        gaze_data = []

        # === Hand tracking ===
        hands_result = self.hands.process(image_rgb)
        if hands_result.multi_hand_landmarks:
            for hand_landmarks in hands_result.multi_hand_landmarks:
                coords = [[lm.x * w, lm.y * h, lm.z * w] for lm in hand_landmarks.landmark]
                hands_data.append(coords)
                self.mp_drawing.draw_landmarks(
                    annotated_image, hand_landmarks, mp.solutions.hands.HAND_CONNECTIONS
                )

        # === Pose (skeleton) tracking ===
        pose_result = self.pose.process(image_rgb)
        if pose_result.pose_landmarks:
            coords = [[lm.x * w, lm.y * h, lm.z * w] for lm in pose_result.pose_landmarks.landmark]
            skeleton_data = coords
            self.mp_drawing.draw_landmarks(
                annotated_image,
                pose_result.pose_landmarks,
                mp.solutions.pose.POSE_CONNECTIONS,
                self.mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2, circle_radius=2),
                self.mp_drawing.DrawingSpec(color=(255, 0, 0), thickness=2)
            )

        # === Face Mesh + Gaze tracking ===
        face_result = self.face_mesh.process(image_rgb)
        if face_result.multi_face_landmarks:
            for face_landmarks in face_result.multi_face_landmarks:
                left_eye_idxs = [33, 133, 159, 145]
                left_iris_idx = 468
                right_eye_idxs = [362, 263, 386, 374]
                right_iris_idx = 473

                def extract_eye_data(eye_indices, iris_index):
                    eye_points = [face_landmarks.landmark[i] for i in eye_indices + [iris_index]]
                    eye_coords = np.array([[p.x * w, p.y * h] for p in eye_points])
                    eye_center = np.mean(eye_coords[:4], axis=0)
                    iris_center = eye_coords[4]
                    gaze_vector = iris_center - eye_center
                    gaze_endpoint = eye_center + 6 * gaze_vector
                    return [eye_center.tolist(), iris_center.tolist(), gaze_vector.tolist()]

                left_eye_data = extract_eye_data(left_eye_idxs, left_iris_idx)
                right_eye_data = extract_eye_data(right_eye_idxs, right_iris_idx)
                gaze_data = [left_eye_data, right_eye_data]  # 2 eyes × (center, iris, vector)

                # Optional: draw gaze on image
                for eye in [left_eye_data, right_eye_data]:
                    eye_center, iris_center, gaze_vector = map(np.array, eye)
                    gaze_endpoint = eye_center + 6 * gaze_vector
                    cv2.circle(annotated_image, tuple(eye_center.astype(int)), 5, (0, 255, 255), -1)
                    cv2.circle(annotated_image, tuple(iris_center.astype(int)), 5, (255, 0, 255), -1)
                    cv2.arrowedLine(
                        annotated_image,
                        tuple(eye_center.astype(int)),
                        tuple(gaze_endpoint.astype(int)),
                        (0, 255, 255), 3, tipLength=0.4
                    )

        return annotated_image, {
            "hands": hands_data,
            "skeleton": skeleton_data,
            "gaze": gaze_data
        }

    