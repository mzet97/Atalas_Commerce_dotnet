// Placeholder visual honesto: bloco neutro com glifo de caixa (sem foto fake).
export function ProductThumb({ big = false, label = "" }: { big?: boolean; label?: string }) {
  return (
    <div className={big ? "thumb big" : "thumb"} role="img" aria-label={label}>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.4" aria-hidden="true">
        <path d="M12 2.5 21 7.5v9l-9 5-9-5v-9l9-5Z" strokeLinejoin="round" />
        <path d="M3.5 7.7 12 12.2l8.5-4.5M12 12.2v9" strokeLinejoin="round" />
      </svg>
    </div>
  );
}
