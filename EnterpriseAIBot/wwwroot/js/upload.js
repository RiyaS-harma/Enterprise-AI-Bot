(() => {
  'use strict';

  const form          = document.getElementById('upload-form');
  const fileInput     = document.getElementById('file-input');
  const dropZone      = document.getElementById('drop-zone');
  const fileNameEl    = document.getElementById('file-name');
  const btnUpload     = document.getElementById('btn-upload');
  const uploadLabel   = document.getElementById('upload-label');
  const uploadSpinner = document.getElementById('upload-spinner');
  const errorEl       = document.getElementById('upload-error');
  const successEl     = document.getElementById('upload-success');
  const resultEl      = document.getElementById('upload-result');
  const resultMsg     = document.getElementById('result-message');
  const resultChunks  = document.getElementById('result-chunks');
  const btnSync       = document.getElementById('btn-sync-existing');

  if (!form || !fileInput || !dropZone || !fileNameEl || !btnUpload || !uploadLabel || !uploadSpinner || !errorEl || !successEl || !resultEl || !resultMsg || !resultChunks) {
    return;
  }

  // ── Drag & Drop ──────────────────────────────────────────────

  dropZone.addEventListener('dragover', (e) => {
    e.preventDefault();
    dropZone.classList.add('drag-over');
  });

  dropZone.addEventListener('dragleave', () => dropZone.classList.remove('drag-over'));

  dropZone.addEventListener('drop', (e) => {
    e.preventDefault();
    dropZone.classList.remove('drag-over');
    if (e.dataTransfer.files.length) {
      fileInput.files = e.dataTransfer.files;
      showFileName(e.dataTransfer.files[0].name);
    }
  });

  fileInput.addEventListener('change', () => {
    if (fileInput.files.length) showFileName(fileInput.files[0].name);
  });

  function showFileName(name) {
    fileNameEl.textContent = name;
  }

  // ── Form Submit ──────────────────────────────────────────────

  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    hideMessages();

    if (!fileInput.files.length) {
      showError('Please select a file to upload.');
      return;
    }

    setLoading(true);

    try {
      const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
      const formData = new FormData(form);

      const response = await fetch('/api/documents/upload', {
        method: 'POST',
        headers: { 'RequestVerificationToken': token },
        body: formData
      });

      const data = await response.json();

      if (!response.ok || !data.success) {
        showError(data.message || 'Upload failed. Please try again.');
        return;
      }

      showSuccess('Document uploaded and indexed successfully.');
      resultMsg.textContent = `Document ID: ${data.documentId}`;
      resultChunks.textContent = data.chunksIndexed > 0
        ? `${data.chunksIndexed} chunks indexed.`
        : 'Document was already indexed.';
      resultEl.hidden = false;
      form.reset();
      fileNameEl.textContent = '';

    } catch (err) {
      showError('Failed to reach the server. Please try again.');
    } finally {
      setLoading(false);
    }
  });

  // ── Helpers ──────────────────────────────────────────────────

  function setLoading(on) {
    btnUpload.disabled = on;
    uploadLabel.hidden = on;
    uploadSpinner.hidden = !on;
  }

  function showError(msg) {
    errorEl.textContent = msg;
    errorEl.hidden = false;
  }

  function showSuccess(msg) {
    successEl.textContent = msg;
    successEl.hidden = false;
  }

  function hideMessages() {
    errorEl.hidden = true;
    successEl.hidden = true;
    resultEl.hidden = true;
  }

  if (btnSync) {
    btnSync.addEventListener('click', async () => {
      hideMessages();
      setSyncLoading(true);

      try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
        const response = await fetch('/api/documents/sync-existing', {
          method: 'POST',
          headers: { 'RequestVerificationToken': token }
        });

        const data = await response.json();
        if (!response.ok || !data.success) {
          showError(data.message || 'Sync failed. Please try again.');
          return;
        }

        showSuccess(data.message || 'Sync completed successfully.');
        resultMsg.textContent = `Processed: ${data.processed ?? 0}`;
        resultChunks.textContent = `Skipped: ${data.skipped ?? 0}, Failed: ${data.failed ?? 0}`;
        resultEl.hidden = false;
      } catch (err) {
        showError('Failed to reach the server. Please try again.');
      } finally {
        setSyncLoading(false);
      }
    });
  }

  function setSyncLoading(on) {
    if (!btnSync) return;
    btnSync.disabled = on;
    btnSync.textContent = on ? 'Syncing...' : 'Sync Existing Blobs';
  }
})();
