{
  description = ".NET development environment";

  inputs = {
    flake-parts.url = "github:hercules-ci/flake-parts";
    # nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    # https://github.com/nixos/nixpkgs/issues/483584
    nixpkgs.url = "github:NixOS/nixpkgs/nixpkgs-unstable";
    git-hooks.url = "github:cachix/git-hooks.nix";
    git-hooks.inputs.nixpkgs.follows = "nixpkgs";
    treefmt-nix.url = "github:numtide/treefmt-nix";
  };

  outputs =
    inputs@{ self, ... }:
    inputs.flake-parts.lib.mkFlake { inherit inputs; } {
      imports = [
        inputs.git-hooks.flakeModule
        inputs.treefmt-nix.flakeModule
      ];
      systems = [
        "x86_64-linux"
        "aarch64-linux"
        "aarch64-darwin"
        "x86_64-darwin"
      ];
      perSystem =
        {
          config,
          lib,
          pkgs,
          system,
          ...
        }:
        {
          treefmt = {
            programs.nixfmt.enable = true;
            programs.nixfmt.package = pkgs.nixfmt;
            programs.csharpier.enable = true;

            settings.formatter.ast-grep = {
              command = "${pkgs.bash}/bin/bash";
              options = [
                "-euc"
                ''
                  # ast-grep scan checks the whole project based on sgconfig.yml
                  # treefmt passes individual files but we ignore them and scan all
                  exec ${lib.getExe pkgs.ast-grep} scan --filter require-braces
                ''
                "--" # bash swallows the second argument when using -c
              ];
              includes = [ "*.cs" ];
            };
          };
          pre-commit.settings.hooks = {
            treefmt.enable = true;
          };
          devShells.default = pkgs.mkShell {
            inherit (config.pre-commit) shellHook;
            packages =
              with pkgs;
              [
                ast-grep
                dotnet
                dotnet-ef
                just
                omnisharp-roslyn
                nuget
                temporal-cli
              ]
              ++ config.pre-commit.settings.enabledPackages;
          };
          _module.args.pkgs = import inputs.nixpkgs {
            inherit system;
            overlays = lib.attrValues self.overlays;
          };
        };
      flake.overlays.default = final: prev: {
        dotnet = final.dotnet-sdk_10;
      };
    };
}
