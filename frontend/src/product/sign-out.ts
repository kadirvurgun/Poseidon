import { purgeQualificationRecovery } from './use-qualification-recovery';

// Ends the BFF session: a normal form POST (CSRF token in the body) so the browser follows the
// API's redirect to the identity provider's end-session endpoint and back to /app.
// Returns false when the navigation could not be started (the caller shows an error).
export function submitSignOut(csrfToken: string): boolean {
    // Sign-out ends the owner's session, so no per-tab private command envelope survives it.
    purgeQualificationRecovery(null);
    const form = document.createElement('form');
    form.method = 'POST';
    form.action = '/api/v1/auth/logout';
    const input = document.createElement('input');
    input.type = 'hidden';
    input.name = '__RequestVerificationToken';
    input.value = csrfToken;
    form.append(input);
    document.body.append(form);
    try {
        form.submit();
        return true;
    }
    catch {
        form.remove();
        return false;
    }
}
