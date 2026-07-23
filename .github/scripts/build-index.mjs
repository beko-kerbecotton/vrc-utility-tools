import { mkdir, readFile, writeFile } from "node:fs/promises";

const token = process.env.GITHUB_TOKEN;
const apiHeaders = {
  Accept: "application/vnd.github+json",
  ...(token ? { Authorization: `Bearer ${token}` } : {})
};

const repos = JSON.parse(await readFile(".github/release/repos.json", "utf8"));

const listing = {
  name: "Beko's VPM Listing",
  id: "net.bekobeko.vpm",
  author: "beko",
  url: "https://vpm.bekobeko.net/index.json",
  packages: {}
};

for (const { owner, repo } of repos) {
  const releasesResponse = await fetch(
    `https://api.github.com/repos/${owner}/${repo}/releases`,
    { headers: apiHeaders }
  );
  if (!releasesResponse.ok) {
    throw new Error(
      `Failed to list releases for ${owner}/${repo}: ${releasesResponse.status}`
    );
  }
  const releases = await releasesResponse.json();

  for (const release of releases) {
    if (release.draft) continue;

    const assets = release.assets ?? [];
    const packageJsonAsset = assets.find((asset) => asset.name === "package.json");
    const zipAsset = assets.find((asset) => asset.name.endsWith(".zip"));
    const sha256Asset = assets.find((asset) => asset.name.endsWith(".zip.sha256"));

    if (!packageJsonAsset || !zipAsset) {
      console.warn(
        `Skipping ${owner}/${repo}@${release.tag_name}: missing package.json or zip asset.`
      );
      continue;
    }

    const packageJsonResponse = await fetch(packageJsonAsset.browser_download_url);
    if (!packageJsonResponse.ok) {
      throw new Error(
        `Failed to download package.json for ${owner}/${repo}@${release.tag_name}`
      );
    }
    const packageJson = await packageJsonResponse.json();

    let zipSHA256 = "";
    if (sha256Asset) {
      const shaResponse = await fetch(sha256Asset.browser_download_url);
      const shaText = await shaResponse.text();
      zipSHA256 = shaText.trim().split(/\s+/)[0] ?? "";
    }

    const packageId = packageJson.name;
    listing.packages[packageId] ??= { versions: {} };
    listing.packages[packageId].versions[packageJson.version] = {
      ...packageJson,
      url: zipAsset.browser_download_url,
      ...(zipSHA256 ? { zipSHA256 } : {})
    };
  }
}

await mkdir("public", { recursive: true });
await writeFile("public/index.json", JSON.stringify(listing, null, 2));

const packageCount = Object.keys(listing.packages).length;
console.log(`Wrote public/index.json with ${packageCount} package(s).`);
