namespace Pots.Client.Services;

// Lightweight in-app translation table. Spanish is the canonical source;
// French and English mirror it. Lookup falls back to Spanish then to the
// raw key so an untranslated string is at least readable.
//
// For v1 we translate the most-visible chrome (nav, status buttons, login,
// greetings) — long form pages (Síntomas, Vitales, Acciones) keep Spanish
// labels and will be translated as we go.
public static class Strings
{
    public static string Get(string lang, string key)
    {
        if (Translations.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var v))
            return v;
        if (Translations["es"].TryGetValue(key, out var es))
            return es;
        return key;
    }

    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
    {
        ["es"] = new()
        {
            // Bottom nav
            ["nav.today"] = "Hoy",
            ["nav.log"] = "Registrar",
            ["nav.trends"] = "Tendencias",
            ["nav.me"] = "Yo",

            // Greetings
            ["greet.morning"] = "Buenos días",
            ["greet.afternoon"] = "Buenas tardes",
            ["greet.evening"] = "Buenas noches",

            // Today
            ["today.eyebrow"] = "Hoy",
            ["today.howAreYou"] = "¿Cómo estás ahora?",
            ["today.green"] = "Bien",
            ["today.green.hint"] = "Día estable, sin síntomas notables.",
            ["today.orange"] = "Regular",
            ["today.orange.hint"] = "Algunos síntomas, pero funcional.",
            ["today.red"] = "Mal",
            ["today.red.hint"] = "Síntomas fuertes o episodio.",
            ["today.recorded"] = "Registrado",
            ["today.confirm.title.good"] = "Anotado.",
            ["today.confirm.title.warn"] = "Anotado.",
            ["today.confirm.title.bad"] = "Aquí estamos.",
            ["today.confirm.hint.good"] = "Sigue cuidándote. Nos vemos en la próxima entrada.",
            ["today.confirm.hint.warn"] = "Si quieres, dejamos constancia de los síntomas que notas ahora.",
            ["today.confirm.hint.bad"] = "Lo siento. Si los síntomas son graves o no remiten, considera contactar con tu médico o llamar a emergencias.",
            ["today.switch.label"] = "¿Te equivocaste de botón? Cambia aquí",
            ["today.switch.hint"] = "El que tiene marca es el actual. Toca otro para cambiarlo.",
            ["today.again"] = "Registrar otro estado",
            ["today.delete"] = "Borrar este registro",
            ["today.deleting"] = "Borrando...",
            ["today.addEpisode"] = "Abrir registro del episodio",
            ["today.addSymptoms"] = "Añadir detalle de síntomas →",
            ["today.history24h"] = "Tus últimas 24 h",
            ["today.detail.add"] = "Añadir detalle",
            ["today.detail.title"] = "Detalle del momento",
            ["today.detail.posture"] = "Postura",
            ["today.detail.activity"] = "Qué estabas haciendo",
            ["today.detail.location"] = "Dónde estabas",
            ["today.detail.note"] = "Nota libre",
            ["today.detail.save"] = "Guardar detalle",
            ["today.detail.cancel"] = "Cancelar",
            ["today.detail.saved"] = "Detalle anotado",
            ["today.detail.suggest"] = "¿Quieres añadir contexto? Postura, qué estabas haciendo, una nota.",

            // Login
            ["login.eyebrow"] = "Acceso · sin contraseña",
            ["login.title"] = "Buenos días.",
            ["login.lead"] = "Introduce tu correo y te enviamos un enlace para entrar. Nada de contraseñas que recordar.",
            ["login.email"] = "Tu correo",
            ["login.send"] = "Enviar enlace",
            ["login.sending"] = "Enviando...",
            ["login.sent.title"] = "Revisa tu correo.",
            ["login.sent.lead"] = "Si el email es válido, recibirás un enlace en breve. Tienes 15 minutos para usarlo y solo funciona una vez.",
            ["login.sent.again"] = "prueba con otro email",
            ["login.sent.checkSpam"] = "¿No te llega? Mira la carpeta de spam, o",

            // Profile
            ["profile.eyebrow"] = "Tu perfil",
            ["profile.session"] = "Sesión iniciada como",
            ["profile.yourName"] = "Tu nombre",
            ["profile.edit"] = "Editar",
            ["profile.permissions"] = "Permisos",
            ["profile.permissions.title"] = "Quién puede ver o editar tus datos",
            ["profile.permissions.lead"] = "Por defecto, solo tú. Aquí puedes dar acceso a otra persona y revocarlo cuando quieras.",
            ["profile.permissions.manage"] = "Gestionar permisos →",
            ["profile.signout"] = "Cerrar sesión",

            // Common
            ["loading"] = "Cargando",
            ["moment"] = "Un momento.",
            ["save"] = "Guardar",
            ["saving"] = "Guardando...",
            ["cancel"] = "Cancelar",
            ["back"] = "Volver",
            ["back.to.today"] = "Volver a hoy",
        },
        ["fr"] = new()
        {
            // Bottom nav
            ["nav.today"] = "Aujourd'hui",
            ["nav.log"] = "Saisir",
            ["nav.trends"] = "Tendances",
            ["nav.me"] = "Moi",

            // Greetings
            ["greet.morning"] = "Bonjour",
            ["greet.afternoon"] = "Bon après-midi",
            ["greet.evening"] = "Bonsoir",

            // Today
            ["today.eyebrow"] = "Aujourd'hui",
            ["today.howAreYou"] = "Comment te sens-tu maintenant ?",
            ["today.green"] = "Bien",
            ["today.green.hint"] = "Journée stable, sans symptômes notables.",
            ["today.orange"] = "Moyen",
            ["today.orange.hint"] = "Quelques symptômes, mais fonctionnel.",
            ["today.red"] = "Mauvais",
            ["today.red.hint"] = "Symptômes forts ou épisode.",
            ["today.recorded"] = "Enregistré",
            ["today.confirm.title.good"] = "Noté.",
            ["today.confirm.title.warn"] = "Noté.",
            ["today.confirm.title.bad"] = "On est là pour toi.",
            ["today.confirm.hint.good"] = "Continue à prendre soin de toi. À la prochaine entrée.",
            ["today.confirm.hint.warn"] = "Si tu veux, on prend note des symptômes que tu ressens maintenant.",
            ["today.confirm.hint.bad"] = "Je suis désolée. Si les symptômes sont graves ou ne passent pas, contacte ton médecin ou appelle les urgences.",
            ["today.switch.label"] = "Mauvais bouton ? Change ici",
            ["today.switch.hint"] = "Celui qui est marqué est l'actuel. Touche un autre pour le changer.",
            ["today.again"] = "Saisir un autre état",
            ["today.delete"] = "Supprimer cette saisie",
            ["today.deleting"] = "Suppression...",
            ["today.addEpisode"] = "Ouvrir le registre de l'épisode",
            ["today.addSymptoms"] = "Ajouter détail des symptômes →",
            ["today.history24h"] = "Tes dernières 24 h",
            ["today.detail.add"] = "Ajouter détail",
            ["today.detail.title"] = "Détail du moment",
            ["today.detail.posture"] = "Posture",
            ["today.detail.activity"] = "Que faisais-tu ?",
            ["today.detail.location"] = "Où étais-tu ?",
            ["today.detail.note"] = "Note libre",
            ["today.detail.save"] = "Enregistrer le détail",
            ["today.detail.cancel"] = "Annuler",
            ["today.detail.saved"] = "Détail noté",
            ["today.detail.suggest"] = "Tu veux ajouter du contexte ? Posture, ce que tu faisais, une note.",

            // Login
            ["login.eyebrow"] = "Connexion · sans mot de passe",
            ["login.title"] = "Bonjour.",
            ["login.lead"] = "Indique ton e-mail et on t'envoie un lien pour entrer. Pas de mot de passe à retenir.",
            ["login.email"] = "Ton e-mail",
            ["login.send"] = "Envoyer le lien",
            ["login.sending"] = "Envoi...",
            ["login.sent.title"] = "Vérifie ton courrier.",
            ["login.sent.lead"] = "Si l'e-mail est valide, tu recevras un lien sous peu. Tu as 15 minutes pour l'utiliser et il ne fonctionne qu'une fois.",
            ["login.sent.again"] = "essayer un autre e-mail",
            ["login.sent.checkSpam"] = "Tu ne le reçois pas ? Regarde le dossier spam, ou",

            // Profile
            ["profile.eyebrow"] = "Ton profil",
            ["profile.session"] = "Connectée en tant que",
            ["profile.yourName"] = "Ton prénom",
            ["profile.edit"] = "Modifier",
            ["profile.permissions"] = "Permissions",
            ["profile.permissions.title"] = "Qui peut voir ou modifier tes données",
            ["profile.permissions.lead"] = "Par défaut, toi seule. Ici tu peux donner accès à quelqu'un et le révoquer quand tu veux.",
            ["profile.permissions.manage"] = "Gérer les permissions →",
            ["profile.signout"] = "Se déconnecter",

            // Common
            ["loading"] = "Chargement",
            ["moment"] = "Un instant.",
            ["save"] = "Enregistrer",
            ["saving"] = "Enregistrement...",
            ["cancel"] = "Annuler",
            ["back"] = "Retour",
            ["back.to.today"] = "Retour à aujourd'hui",
        },
        ["en"] = new()
        {
            // Bottom nav
            ["nav.today"] = "Today",
            ["nav.log"] = "Log",
            ["nav.trends"] = "Trends",
            ["nav.me"] = "You",

            // Greetings
            ["greet.morning"] = "Good morning",
            ["greet.afternoon"] = "Good afternoon",
            ["greet.evening"] = "Good evening",

            // Today
            ["today.eyebrow"] = "Today",
            ["today.howAreYou"] = "How are you right now?",
            ["today.green"] = "Good",
            ["today.green.hint"] = "Stable day, no notable symptoms.",
            ["today.orange"] = "Okay",
            ["today.orange.hint"] = "Some symptoms, still functional.",
            ["today.red"] = "Bad",
            ["today.red.hint"] = "Heavy symptoms or an episode.",
            ["today.recorded"] = "Recorded",
            ["today.confirm.title.good"] = "Logged.",
            ["today.confirm.title.warn"] = "Logged.",
            ["today.confirm.title.bad"] = "We're here.",
            ["today.confirm.hint.good"] = "Keep taking care of yourself. See you at the next entry.",
            ["today.confirm.hint.warn"] = "If you want, we can note which symptoms you're feeling now.",
            ["today.confirm.hint.bad"] = "I'm sorry. If symptoms are severe or don't ease, consider calling your doctor or emergency services.",
            ["today.switch.label"] = "Wrong button? Change here",
            ["today.switch.hint"] = "The marked one is the current one. Tap another to change it.",
            ["today.again"] = "Log another state",
            ["today.delete"] = "Delete this entry",
            ["today.deleting"] = "Deleting...",
            ["today.addEpisode"] = "Open episode log",
            ["today.addSymptoms"] = "Add symptom detail →",
            ["today.history24h"] = "Your last 24h",
            ["today.detail.add"] = "Add detail",
            ["today.detail.title"] = "Moment detail",
            ["today.detail.posture"] = "Posture",
            ["today.detail.activity"] = "What you were doing",
            ["today.detail.location"] = "Where you were",
            ["today.detail.note"] = "Free note",
            ["today.detail.save"] = "Save detail",
            ["today.detail.cancel"] = "Cancel",
            ["today.detail.saved"] = "Detail saved",
            ["today.detail.suggest"] = "Want to add context? Posture, what you were doing, a note.",

            // Login
            ["login.eyebrow"] = "Sign in · no password",
            ["login.title"] = "Good morning.",
            ["login.lead"] = "Enter your email and we'll send you a link to sign in. No passwords to remember.",
            ["login.email"] = "Your email",
            ["login.send"] = "Send link",
            ["login.sending"] = "Sending...",
            ["login.sent.title"] = "Check your inbox.",
            ["login.sent.lead"] = "If the email is valid you'll get a link shortly. You have 15 minutes to use it, and it only works once.",
            ["login.sent.again"] = "try another email",
            ["login.sent.checkSpam"] = "Not arriving? Check spam, or",

            // Profile
            ["profile.eyebrow"] = "Your profile",
            ["profile.session"] = "Signed in as",
            ["profile.yourName"] = "Your name",
            ["profile.edit"] = "Edit",
            ["profile.permissions"] = "Permissions",
            ["profile.permissions.title"] = "Who can view or edit your data",
            ["profile.permissions.lead"] = "By default, only you. Here you can give access to someone else and revoke it anytime.",
            ["profile.permissions.manage"] = "Manage permissions →",
            ["profile.signout"] = "Sign out",

            // Common
            ["loading"] = "Loading",
            ["moment"] = "One moment.",
            ["save"] = "Save",
            ["saving"] = "Saving...",
            ["cancel"] = "Cancel",
            ["back"] = "Back",
            ["back.to.today"] = "Back to today",
        },
    };
}
