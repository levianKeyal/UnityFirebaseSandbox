const fs = require("fs");
const path = require("path");
const admin = require("firebase-admin");

const allowedRoles = new Set(["user", "admin", "superAdmin"]);

function printUsage() {
  console.log("Usage: node set-role.js <uid> <user|admin|superAdmin> [--sync-firestore]");
}

function validateArguments(args) {
  if (args.length < 2) {
    return null;
  }

  const uid = String(args[0] || "").trim();
  const role = String(args[1] || "").trim();
  const syncFirestore = args.includes("--sync-firestore");

  if (!uid) {
    throw new Error("UID vacio.");
  }

  if (!allowedRoles.has(role)) {
    throw new Error(`Role invalido '${role}'. Roles validos: user, admin, superAdmin.`);
  }

  return { uid, role, syncFirestore };
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

  const { uid, role, syncFirestore } = parsed;
  const serviceAccount = loadServiceAccount();

  if (!admin.apps.length) {
    admin.initializeApp({
      credential: admin.credential.cert(serviceAccount),
      projectId: serviceAccount.project_id
    });
  }

  console.log(`Asignando custom claim role='${role}' a UID ${uid}...`);
  await admin.auth().setCustomUserClaims(uid, { role });

  if (syncFirestore) {
    const userRef = admin.firestore().collection("users").doc(uid);
    const snapshot = await userRef.get();

    if (snapshot.exists) {
      console.log("Sincronizando role en Firestore...");
      await userRef.set({ role }, { merge: true });
    } else {
      console.warn(
        "El documento users/{uid} no existe. El claim se actualizo, pero Firestore no se sincronizo porque el perfil aun no existe."
      );
    }
  }

  console.log("Listo.");
  console.log("Recuerda renovar el token del usuario con logout/login o refresh forzado.");
}

main().catch((error) => {
  console.error(error && error.stack ? error.stack : error);
  process.exitCode = 1;
});
