#include <assert.h>
#include <stdarg.h>
#include <stdint.h>
#include <stdio.h>
#include "mouse_capture.h"
#include "options.h"

// Link the production handler against side-effect-free SDL/Win32 fixtures.
bool ez_capture_active;
static bool relative_mode, button_capture, capture_allowed;
static unsigned mode_changes, release_notifications, cases;
static SDL_Window *focus;
static SDL_Window *input_window = (SDL_Window *)(uintptr_t) 1;

bool SDL_GetWindowRelativeMouseMode(SDL_Window *window) {
    (void) window;
    return relative_mode;
}
bool SDL_SetWindowRelativeMouseMode(SDL_Window *window, bool enabled) {
    (void) window;
    relative_mode = enabled;
    ++mode_changes;
    return true;
}
SDL_Window *SDL_GetKeyboardFocus(void) { return focus; }
const char *SDL_GetError(void) { return "capture fixture"; }
void SDL_LogError(int category, const char *format, ...) {
    (void) category;
    (void) format;
}
bool ezacross_capture_mouse(SDL_Window *window, bool active) {
    if (active && (!capture_allowed || focus != window)) return false;
    button_capture = active;
    return true;
}
void ezacross_capture_lost(void) {
    ez_capture_active = false;
    ++release_notifications;
}
static void reset(struct sc_mouse_capture *capture, bool active) {
    relative_mode = button_capture = ez_capture_active = active;
    capture_allowed = true;
    focus = input_window;
    mode_changes = release_notifications = 0;
    sc_mouse_capture_init(capture, input_window, SC_SHORTCUT_MOD_LALT);
}
static void passed(const char *name) {
    ++cases;
    printf("{\"case\":\"%s\",\"passed\":true}\n", name);
}
int main(void) {
    struct sc_mouse_capture capture;
    SDL_Event event = {0};
    const unsigned buttons[] = { SDL_BUTTON_LEFT, SDL_BUTTON_RIGHT, SDL_BUTTON_MIDDLE };
    const char *names[] = { "left_click_keeps_capture", "right_click_keeps_capture", "middle_click_keeps_capture" };
    for (unsigned i = 0; i < 3; ++i) {
        reset(&capture, true);
        event.type = SDL_EVENT_MOUSE_BUTTON_DOWN;
        event.button.button = buttons[i];
        assert(!sc_mouse_capture_handle_event(&capture, &event));
        event.type = SDL_EVENT_MOUSE_MOTION;
        assert(!sc_mouse_capture_handle_event(&capture, &event));
        event.type = SDL_EVENT_MOUSE_BUTTON_UP;
        assert(!sc_mouse_capture_handle_event(&capture, &event));
        assert(relative_mode && button_capture && ez_capture_active);
        assert(release_notifications == 0);
        passed(names[i]);
    }
    reset(&capture, true);
    event.type = SDL_EVENT_MOUSE_WHEEL;
    assert(!sc_mouse_capture_handle_event(&capture, &event));
    assert(relative_mode && button_capture);
    passed("scroll_keeps_capture");

    reset(&capture, true);
    event.type = SDL_EVENT_WINDOW_FOCUS_LOST;
    assert(sc_mouse_capture_handle_event(&capture, &event));
    assert(relative_mode && ez_capture_active && release_notifications == 0);
    passed("queued_old_focus_loss_does_not_release_current_focus");

    reset(&capture, true);
    focus = NULL;
    event.type = SDL_EVENT_WINDOW_FOCUS_LOST;
    assert(sc_mouse_capture_handle_event(&capture, &event));
    assert(!relative_mode && !button_capture && !ez_capture_active);
    assert(release_notifications == 1);
    assert(sc_mouse_capture_handle_event(&capture, &event));
    assert(release_notifications == 1 && mode_changes == 1);
    passed("real_focus_loss_releases_and_notifies_once");
    event.type = SDL_EVENT_MOUSE_BUTTON_DOWN;
    assert(sc_mouse_capture_handle_event(&capture, &event));
    event.type = SDL_EVENT_MOUSE_BUTTON_UP;
    assert(sc_mouse_capture_handle_event(&capture, &event));
    assert(!relative_mode && !ez_capture_active);
    passed("click_after_focus_loss_cannot_recapture_without_host");

    reset(&capture, true);
    ez_capture_active = false; // CAPTURE_OFF changes host intent first.
    sc_mouse_capture_set_active(&capture, false);
    event.type = SDL_EVENT_MOUSE_BUTTON_UP;
    assert(sc_mouse_capture_handle_event(&capture, &event));
    assert(!relative_mode && !button_capture);
    sc_mouse_capture_set_active(&capture, false);
    assert(mode_changes == 1);
    passed("explicit_release_keeps_no_duplicate_cursor_warp");

    reset(&capture, false);
    sc_mouse_capture_set_active(&capture, true);
    assert(relative_mode && button_capture);
    sc_mouse_capture_set_active(&capture, true);
    assert(mode_changes == 1);
    passed("capture_routes_buttons_without_reapplying_relative_mode");

    reset(&capture, false);
    capture_allowed = false;
    sc_mouse_capture_set_active(&capture, true);
    assert(!relative_mode && !button_capture);
    passed("button_capture_failure_rolls_back_relative_mode");

    reset(&capture, false);
    focus = NULL;
    sc_mouse_capture_set_active(&capture, true);
    assert(!relative_mode && !button_capture);
    passed("unfocused_window_cannot_claim_capture");

    reset(&capture, true);
    event.type = SDL_EVENT_KEY_DOWN;
    event.key.key = SDLK_LALT;
    assert(sc_mouse_capture_handle_event(&capture, &event));
    event.type = SDL_EVENT_KEY_UP;
    assert(sc_mouse_capture_handle_event(&capture, &event));
    assert(!relative_mode && !ez_capture_active && release_notifications == 1);
    passed("shortcut_release_notifies_host");
    event.type = SDL_EVENT_KEY_DOWN;
    assert(sc_mouse_capture_handle_event(&capture, &event));
    event.type = SDL_EVENT_KEY_UP;
    assert(sc_mouse_capture_handle_event(&capture, &event));
    assert(!relative_mode && !ez_capture_active && release_notifications == 1);
    passed("shortcut_cannot_recapture_after_release");
    printf("{\"passed\":%u,\"failed\":0,\"uses_real_handler\":true,\"physical_input\":false}\n", cases);
    return 0;
}
