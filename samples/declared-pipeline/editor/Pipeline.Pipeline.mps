<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="4ff5bb38-8734-42b7-9740-69a08a3a740d" name="Pipeline.Pipeline">
  <nodes>
    <node id="9da30956-b600-4bbf-8e1b-fb63871deb62" concept="Editor.Notation" name="Pipeline.Pipeline">
      <properties>
        <conceptKey>Pipeline.Pipeline</conceptKey>
        <variant>default</variant>
      </properties>
      <children role="root">
        <node id="1c236545-27db-4d90-ade2-ee93352ae1ca" concept="Editor.Sequence">
          <children role="cells">
            <node id="1ae2d608-ca7b-4454-988c-e871e7ba992c" concept="Editor.Line">
              <children role="cells">
                <node id="8cb6fca8-0f2b-4ed8-a42b-8ba89528b418" concept="Editor.Keyword" name="pipeline">
                  <properties>
                    <text>pipeline</text>
                  </properties>
                </node>
                <node id="73c9a34b-1a0d-4002-a51c-eee1aa2ede7b" concept="Editor.Space" />
                <node id="289506e6-5a91-4f19-9901-d54cac3e09e3" concept="Editor.Property" name="name">
                  <properties>
                    <slot>name</slot>
                  </properties>
                </node>
              </children>
            </node>
            <node id="15438f4f-8c5d-476c-b84f-18fc2e871f5e" concept="Editor.Indent">
              <children role="cells">
                <node id="f8887656-0650-4b1e-a778-202f2d784a87" concept="Editor.Table" name="stages">
                  <properties>
                    <slot>stages</slot>
                    <emptyText>+ stage</emptyText>
                  </properties>
                  <children role="columns">
                    <node id="c4f3d55f-9632-47b3-9c76-e78130fe7b40" concept="Editor.Column" name="name">
                      <properties>
                        <slot>name</slot>
                        <emptyText>&lt;empty&gt;</emptyText>
                      </properties>
                      <children role="header">
                        <node id="cec90d86-11f8-4784-a797-c589a5307881" concept="Editor.Keyword" name="stage">
                          <properties>
                            <text>stage</text>
                          </properties>
                        </node>
                      </children>
                    </node>
                    <node id="635f772a-d290-498f-b51e-9f151a61342d" concept="Editor.Column" name="command">
                      <properties>
                        <slot>command</slot>
                        <emptyText>&lt;empty&gt;</emptyText>
                      </properties>
                      <children role="header">
                        <node id="81c893f9-eb95-40ad-8381-2db259a2c8e8" concept="Editor.Keyword" name="run">
                          <properties>
                            <text>run</text>
                          </properties>
                        </node>
                      </children>
                    </node>
                    <node id="9b1dee7e-37eb-4717-b250-018a86227a1d" concept="Editor.Column" name="retries">
                      <properties>
                        <slot>retries</slot>
                        <emptyText>&lt;empty&gt;</emptyText>
                      </properties>
                      <children role="header">
                        <node id="4ee07491-504d-4ca2-bb0c-c83c862cc18a" concept="Editor.Keyword" name="retries">
                          <properties>
                            <text>retries</text>
                          </properties>
                        </node>
                      </children>
                    </node>
                  </children>
                </node>
              </children>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>