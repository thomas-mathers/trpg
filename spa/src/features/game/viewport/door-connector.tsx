function DoorFrame() {
  return (
    <group>
      {[-0.7, 0.7].map((x) => (
        <mesh castShadow receiveShadow key={x} position={[x, 1.25, 0]}>
          <boxGeometry args={[0.18, 2.5, 0.3]} />
          <meshStandardMaterial color="#65503a" roughness={0.9} />
        </mesh>
      ))}
      <mesh castShadow receiveShadow position={[0, 2.53, 0]}>
        <boxGeometry args={[1.62, 0.22, 0.36]} />
        <meshStandardMaterial color="#806548" roughness={0.9} />
      </mesh>
      <mesh castShadow receiveShadow position={[0, 0.045, 0]}>
        <boxGeometry args={[1.6, 0.09, 0.55]} />
        <meshStandardMaterial color="#999080" roughness={1} />
      </mesh>
    </group>
  );
}

function DoorLeaf() {
  return (
    <group position={[0, 0, 0.08]}>
      {[-2, -1, 0, 1, 2].map((i) => (
        <mesh castShadow receiveShadow key={i} position={[i * 0.24, 1.23, 0]}>
          <boxGeometry args={[0.23, 2.3, 0.09]} />
          <meshStandardMaterial color={i % 2 ? '#705035' : '#7e5c3b'} roughness={0.85} />
        </mesh>
      ))}
      {[0.55, 1.9].map((height) => (
        <mesh castShadow receiveShadow key={height} position={[0, height, 0.055]}>
          <boxGeometry args={[1.16, 0.085, 0.035]} />
          <meshStandardMaterial color="#333c40" metalness={0.7} roughness={0.5} />
        </mesh>
      ))}
      <mesh castShadow receiveShadow position={[0.37, 1.17, 0.1]}>
        <torusGeometry args={[0.085, 0.018, 8, 16]} />
        <meshStandardMaterial color="#d2ac63" metalness={0.7} roughness={0.3} />
      </mesh>
    </group>
  );
}

export function DoorConnector() {
  return (
    <group>
      <DoorFrame />
      <DoorLeaf />
      <mesh castShadow receiveShadow position={[0, 2.53, 0.19]}>
        <boxGeometry args={[0.38, 0.045, 0.015]} />
        <meshStandardMaterial color="#f5d88e" emissive="#e7b756" emissiveIntensity={0.65} />
      </mesh>
    </group>
  );
}
