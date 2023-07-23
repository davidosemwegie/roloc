module.exports = function (api) {
  api.cache(true);
  return {
    presets: ['babel-preset-expo'],
    plugins: ["nativewind/babel",
      [
        'module-resolver',
        {
          root: ['.'],
          alias: {
            '@layouts': "./src/layouts",
            '@components': "./src/components",
            '@screens': "./src/screens",
            '@assets': "./src/assets",
            '@utils': "./src/utils",
            "@types": "./src/types",
            "@stores": "./src/stores",
            "local-storage": "./src/utils/local-storage",
          },
        },
      ]

    ]
  };
};
