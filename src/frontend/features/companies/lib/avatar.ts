const AVATAR_PALETTE: [string, string][] = [
    ["#6C5BD9", "#9B8CF0"],
    ["#4C8DF6", "#7FB0FA"],
    ["#E16BA0", "#F09BC2"],
    ["#2FB36F", "#73D6A0"],
    ["#F0863C", "#F7B07A"],
    ["#1E9FB0", "#6FCBD6"],
    ["#8A5BD9", "#B79BFF"],
];

function hashSeed(s: string): number {
    let h = 0;
    for (let i = 0; i < s.length; i++) h = (Math.imul(31, h) + s.charCodeAt(i)) | 0;
    return Math.abs(h);
}

export function ava(seed: string): { from: string; to: string } {
    const [from, to] = AVATAR_PALETTE[hashSeed(seed) % AVATAR_PALETTE.length];
    return { from, to };
}

export function initials(title: string): string {
    return title
        .split(/\s+/)
        .slice(0, 2)
        .map((w) => w[0]?.toUpperCase() ?? "")
        .join("");
}
