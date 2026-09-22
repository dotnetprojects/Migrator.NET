// The documentation remains readable and navigable without JavaScript.
const status = document.getElementById("copy-status");
if (navigator.clipboard && window.isSecureContext) {
  document.querySelectorAll("[data-copy]").forEach((button) => {
    button.hidden = false;
    button.addEventListener("click", async () => {
      try {
        await navigator.clipboard.writeText(
          document.getElementById(button.dataset.copy).textContent,
        );
        button.textContent = "Copied";
        status.textContent = "Code copied to clipboard.";
        setTimeout(() => {
          button.textContent = "Copy";
        }, 2000);
      } catch {
        status.textContent =
          "Unable to copy. Select the code and copy it manually.";
      }
    });
  });
}

// Open the source notes when following a direct citation or shared fragment.
function revealSource() {
  const id = window.location.hash.slice(1);
  if (id === "sources" || id.startsWith("source-")) {
    document.getElementById("sources").open = true;
    document.getElementById(id)?.scrollIntoView();
  }
}
window.addEventListener("hashchange", revealSource);
revealSource();
