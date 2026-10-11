(() => {
  'use strict';

  const loginForm = document.getElementById('login-form');
  const loginBtn = document.getElementById('btn-login');
  const loginError = document.getElementById('login-error');

  const logoutBtn = document.getElementById('btn-logout');
  const logoutForm = document.getElementById('logout-form');

  const employeeRegisterForm = document.getElementById('employee-register-form');
  const employeeRegisterBtn = document.getElementById('btn-register-employee');
  const employeeRegisterSuccess = document.getElementById('employee-register-success');
  const employeeRegisterError = document.getElementById('employee-register-error');

  if (loginForm) {
    loginForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      hideLoginError();

      const email = document.getElementById('login-email')?.value?.trim() || '';
      const password = document.getElementById('login-password')?.value || '';
      const token = loginForm.querySelector('input[name="__RequestVerificationToken"]')?.value;

      if (!email || !password) {
        showLoginError('Email and password are required.');
        return;
      }

      loginBtn.disabled = true;
      loginBtn.textContent = 'Signing in...';

      try {
        const response = await fetch('/api/auth/login', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': token || ''
          },
          body: JSON.stringify({ email, password })
        });

        if (!response.ok) {
          const data = await response.json().catch(() => ({}));
          throw new Error(data.error || 'Sign in failed.');
        }

        window.location.reload();
      } catch (err) {
        showLoginError(err.message || 'Unable to sign in. Please try again.');
      } finally {
        loginBtn.disabled = false;
        loginBtn.textContent = 'Sign In';
      }
    });
  }

  if (logoutBtn && logoutForm) {
    logoutBtn.addEventListener('click', async () => {
      const token = logoutForm.querySelector('input[name="__RequestVerificationToken"]')?.value;
      logoutBtn.disabled = true;

      try {
        await fetch('/api/auth/logout', {
          method: 'POST',
          headers: {
            'RequestVerificationToken': token || ''
          }
        });
      } finally {
        window.location.href = '/Chat';
      }
    });
  }

  if (employeeRegisterForm && employeeRegisterBtn) {
    employeeRegisterForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      hideRegisterBanners();

      const employeeId = document.getElementById('employee-id')?.value?.trim() || '';
      const fullName = document.getElementById('employee-full-name')?.value?.trim() || '';
      const email = document.getElementById('employee-email')?.value?.trim() || '';
      const department = document.getElementById('employee-department')?.value?.trim() || '';
      const role = document.getElementById('employee-role')?.value?.trim() || 'Employee';
      const password = document.getElementById('employee-password')?.value || '';
      const confirmPassword = document.getElementById('employee-confirm-password')?.value || '';
      const token = employeeRegisterForm.querySelector('input[name="__RequestVerificationToken"]')?.value;

      if (!employeeId || !fullName || !email || !department || !password || !confirmPassword) {
        showRegisterError('All fields are required.');
        return;
      }

      if (password !== confirmPassword) {
        showRegisterError('Password and confirm password do not match.');
        return;
      }

      employeeRegisterBtn.disabled = true;
      employeeRegisterBtn.textContent = 'Registering...';

      try {
        const response = await fetch('/api/auth/register', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': token || ''
          },
          body: JSON.stringify({
            email,
            password,
            displayName: fullName,
            role
          })
        });

        const data = await response.json().catch(() => ({}));
        if (!response.ok) {
          const details = Array.isArray(data.errors) ? data.errors.join(' ') : data.error;
          throw new Error(details || 'Failed to register employee.');
        }

        showRegisterSuccess(data.message || 'Employee registered successfully.');
        employeeRegisterForm.reset();
      } catch (err) {
        showRegisterError(err.message || 'Unable to register employee. Please try again.');
      } finally {
        employeeRegisterBtn.disabled = false;
        employeeRegisterBtn.textContent = 'Register';
      }
    });
  }


  function showLoginError(message) {
    if (!loginError) return;
    loginError.textContent = message;
    loginError.hidden = false;
  }

  function hideLoginError() {
    if (!loginError) return;
    loginError.hidden = true;
    loginError.textContent = '';
  }

  function showRegisterSuccess(message) {
    if (!employeeRegisterSuccess) return;
    employeeRegisterSuccess.textContent = message;
    employeeRegisterSuccess.hidden = false;
  }

  function showRegisterError(message) {
    if (!employeeRegisterError) return;
    employeeRegisterError.textContent = message;
    employeeRegisterError.hidden = false;
  }

  function hideRegisterBanners() {
    if (employeeRegisterSuccess) {
      employeeRegisterSuccess.hidden = true;
      employeeRegisterSuccess.textContent = '';
    }

    if (employeeRegisterError) {
      employeeRegisterError.hidden = true;
      employeeRegisterError.textContent = '';
    }
  }

})();
