const fs = require("fs");
const path = require("path");
const admin = require("firebase-admin");

const allowedStatuses = new Set(["active", "suspended", "disabled"]);

function printUsage() {
  console.log("Usage: node set-status.js <uid> <active|suspended|disabled>");
}

function validateArguments(args) {
  if (args.length < 2) {
    return null;
  }

  const uid = String(args[0] || "").trim();
  const status = String(args[1] || "").trim();

  if (!uid) {
    throw new Error("UID vacio.");
  }

  if (!allowedStatuses.has(status)) {
    throw new Error(
      `Status invalido '${status}'. Estados validos: active, suspended, disabled.`
    );
  }

  return { uid, status };
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
  const parsed = validateArguments(process.argv.slice(2));

  if (!parsed) {
    printUsage();
    process.exitCode = 1;
    return;
  }

  const { uid, status } = parsed;
  const serviceAccount = loadServiceAccount();

  if (!admin.apps.length) {
    admin.initializeApp({
      credential: admin.credential.cert(serviceAccount),
      projectId: serviceAccount.project_id
    });
  }

  await admin.auth().getUser(uid);

  const userRef = admin.firestore().collection("users").doc(uid);
  const snapshot = await userRef.get();

  if (!snapshot.exists) {
    throw new Error(
      "El documento users/{uid} no existe. No se actualizo el status porque el perfil aun no existe."
    );
  }

  console.log(`Actualizando status='${status}' en Firestore para UID ${uid}...`);
  await userRef.set({ status }, { merge: true });

  console.log("Listo.");
}

main().catch((error) => {
  console.error(error && error.stack ? error.stack : error);
  process.exitCode = 1;
});
