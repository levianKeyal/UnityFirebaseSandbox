const fs = require("fs");
const path = require("path");
const admin = require("firebase-admin");

function printUsage() {
  console.log("Usage: node check-user.js <uid>");
}

function loadServiceAccount() {
  const serviceAccountPath = path.join(__dirname, "serviceAccountKey.json");

  if (!fs.existsSync(serviceAccountPath)) {
    throw new Error(
      "No se encontro serviceAccountKey.json en Tools/FirebaseAdmin/. Descargalo desde Firebase Console y guardalo localmente."
    );
  }

  return JSON.parse(fs.readFileSync(serviceAccountPath, "utf8"));
}

async function main() {
  const uid = String(process.argv[2] || "").trim();

  if (!uid) {
    printUsage();
    process.exitCode = 1;
    return;
  }

  const serviceAccount = loadServiceAccount();

  if (!admin.apps.length) {
    admin.initializeApp({
      credential: admin.credential.cert(serviceAccount),
      projectId: serviceAccount.project_id
    });
  }

  const userRecord = await admin.auth().getUser(uid);
  const userDoc = await admin.firestore().collection("users").doc(uid).get();

  console.log(`UID: ${userRecord.uid}`);
  console.log(`Project ID: ${serviceAccount.project_id}`);
  console.log("Custom Claims:");
  console.log(JSON.stringify(userRecord.customClaims || {}, null, 2));

  console.log("Firestore Profile:");
  if (!userDoc.exists) {
    console.log("(no existe)");
    return;
  }

  const profileData = userDoc.data() || {};
  console.log(
    JSON.stringify(
      {
        uid: profileData.uid || "",
        email: profileData.email || "",
        displayName: profileData.displayName || "",
        photoUrl: profileData.photoUrl || "",
        role: profileData.role || "",
        status: profileData.status || "",
        createdAt: profileData.createdAt || null,
        lastLogin: profileData.lastLogin || null
      },
      null,
      2
    )
  );
}

main().catch((error) => {
  console.error(error && error.stack ? error.stack : error);
  process.exitCode = 1;
});
