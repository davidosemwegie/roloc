import { initializeApp } from 'firebase/app';
import { getAnalytics, logEvent } from "firebase/analytics";

// Optionally import the services that you want to use
// import {...} from "firebase/auth";
// import {...} from "firebase/database";
// import {...} from "firebase/firestore";
// import {...} from "firebase/functions";
// import {...} from "firebase/storage";

// Initialize Firebase
const firebaseConfig = {
    apiKey: "AIzaSyChqCJ2GgVlPzi8p1MuFYgXRIQS3D8EpoA",
    authDomain: "roloc-app.firebaseapp.com",
    projectId: "roloc-app",
    storageBucket: "roloc-app.appspot.com",
    messagingSenderId: "637282075027",
    appId: "1:637282075027:web:c1a8986a8108960d7aa866",
    measurementId: "G-L65C00PFYM"
};
// Initialize Firebase
const app = initializeApp(firebaseConfig);
export const analytics = getAnalytics(app);// For more information on how to access Firebase in your project,
// see the Firebase documentation: https://firebase.google.com/docs/web/setup#access-firebase

export const trackEvent = (name: string, options?: any) => logEvent(analytics, name, options)
